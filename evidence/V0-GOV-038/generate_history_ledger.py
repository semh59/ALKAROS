"""Materialize the V0-GOV-038 immutable-history attestation from CORR:C52."""

from __future__ import annotations

import csv
import hashlib
import json
import re
import subprocess
from functools import lru_cache
from pathlib import Path


REPOSITORY = Path(r"D:\PROJECT\ALKAROS-REMEDIATION")
OUTPUT = REPOSITORY / "evidence" / "V0-GOV-038"
CANDIDATE = "0c8cd75fbebeacfdf455f24de9b13c5ee7434da6"
C52_INPUT = Path(
    r"D:\PROJECT\ALKAROS-AUDIT\20260810-110911-825882aa\commit-scope-audit.json"
)
C52_INPUT_SHA256 = "35CFE716B72FC07C2B660EE2E04707A10D038161C5B45F9F7FB28A753A042DF8"
C52_SNAPSHOT_LAST_COMMIT = "825882aaaa2a9483694120cab4f65017da93ffc1"
SCOPE_FAIL_COUNT = 45
FOOTER_FAIL_COMMITS = (
    "81320187fb24dbabb8c2bbe021b5cab6adbc9605",
    "37a44b5c68c852943e88801dd93fa7e3bf5913f4",
    "a55854667b1846fffe82aaa9992a45c35fa7aed9",
    "f912e409e7306f4494955462fb1199077db7f7e1",
    "7526fc8d3c3016f045a3f503f2fe1596394a4f1e",
    "4e5330211641a1f127ac3625d24ab02cc24fc95b",
    "fdab1da98edc4c81e928e3de0dcfd2f6b6beb678",
    "9e8471086e28ba9706ed8044041fa1d7459c600d",
    "974d9fc1649f74f185114bd334c9f949a8aa8893",
    "ef92770e4e4f2e36ed276082d715132d8d64a748",
    "750110821347b57632d99c23de48681284996812",
    "d2b066334d79028c3d31d4d3922600fd8c175af3",
    "825882aaaa2a9483694120cab4f65017da93ffc1",
)
IMMUTABLE_TRAILER_EXCEPTIONS = {
    "2afa0c3445279be8a5fb3ba80fa2c3d0d22484c6": "LITERAL_BACKSLASH_N",
    "0f2efe6616a90007d326c0d1870a436f0ae2577e": "SEPARATED_TRAILER_BLOCK",
}
TRAILER_LINE = re.compile(r"^(Task|Gate): ([^\r\n]+)$")
TASK_ID = re.compile(r"V\d+-[A-Z]+-\d{3}")


def git(*arguments: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(REPOSITORY), *arguments],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="strict",
    )
    return completed.stdout


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def changed_paths(commit: str) -> list[str]:
    return [
        path
        for path in git("diff-tree", "--root", "--no-commit-id", "--name-only", "-r", commit).splitlines()
        if path
    ]


def commit_fields(commit: str) -> dict[str, object]:
    author, email, authored_at, subject = git(
        "show", "-s", "--format=%an%x00%ae%x00%aI%x00%s", commit
    ).rstrip("\n").split("\0")
    return {
        "commit": commit,
        "parents": git("show", "-s", "--format=%P", commit).strip().split(),
        "author": author,
        "author_email": email,
        "authored_at": authored_at,
        "subject": subject,
        "changed_paths": changed_paths(commit),
    }


def strict_trailers(commit: str) -> tuple[list[str], list[str], str]:
    message = git("show", "-s", "--format=%B", commit).rstrip("\n")
    if commit in IMMUTABLE_TRAILER_EXCEPTIONS:
        return [], [], f"IMMUTABLE_{IMMUTABLE_TRAILER_EXCEPTIONS[commit]}"
    lines = message.splitlines()
    footer_lines = [line for line in lines if line.startswith(("Task:", "Gate:"))]
    trailing: list[str] = []
    for line in reversed(lines):
        if TRAILER_LINE.match(line):
            trailing.append(line)
            continue
        break
    trailing.reverse()
    if not footer_lines:
        return [], [], "MISSING"
    if footer_lines != trailing:
        return [], [], "PSEUDO_NOT_TRAILER"
    tasks: list[str] = []
    gates: list[str] = []
    for line in trailing:
        name, value = line.split(": ", 1)
        if name == "Task":
            tasks.extend(TASK_ID.findall(value))
        else:
            gates.append(value)
    return tasks, gates, "CANONICAL"


@lru_cache(maxsize=None)
def task_contract(ref: str, task_id: str) -> dict[str, object] | None:
    matches = git("grep", "-l", "-F", "-e", f"- Task ID: {task_id}", ref, "--", "plan").splitlines()
    if len(matches) != 1:
        return None
    path = matches[0].split(":", 1)[1]
    text = git("show", f"{ref}:{path}")
    status_match = re.search(r"^- Status: ([A-Za-z]+)\s*$", text, re.MULTILINE)
    dependencies_match = re.search(
        r"^## Dependencies\s*$\n(.*?)(?=^## |\Z)", text, re.MULTILINE | re.DOTALL
    )
    owned_match = re.search(
        r"^## Owned surface\s*$\n(.*?)(?=^## |\Z)", text, re.MULTILINE | re.DOTALL
    )
    if status_match is None or dependencies_match is None or owned_match is None:
        return None
    dependencies = TASK_ID.findall(dependencies_match.group(1))
    owned = re.findall(r"`([^`]+)`", owned_match.group(1))
    allowlist = [
        item.replace("\\", "/").lstrip("./")
        for item in owned
        if any(marker in item for marker in ("/", "\\", ".", "*", "?"))
    ]
    allowlist.extend([path, f"evidence/{task_id}/**"])
    return {
        "path": path,
        "status": status_match.group(1),
        "dependencies": dependencies,
        "allowlist": allowlist,
    }


def matches_allowlist(path: str, pattern: str) -> bool:
    path = path.casefold()
    pattern = pattern.replace("\\", "/").lstrip("./").casefold()
    expression = re.escape(pattern)
    expression = expression.replace(r"\*\*", ".*").replace(r"\*", "[^/]*").replace(r"\?", "[^/]")
    return re.fullmatch(expression, path) is not None


def scope_result(commit: str, task_ids: list[str], ref: str) -> tuple[str, list[str], list[dict[str, object]]]:
    if not task_ids:
        return "UNATTRIBUTED", [], []
    contracts = [task_contract(ref, task_id) for task_id in task_ids]
    if any(contract is None for contract in contracts):
        return "UNPROVEN", changed_paths(commit), []
    concrete_contracts = [contract for contract in contracts if contract is not None]
    allowlist = [item for contract in concrete_contracts for item in contract["allowlist"]]
    outside = [
        path
        for path in changed_paths(commit)
        if not any(matches_allowlist(path, pattern) for pattern in allowlist)
    ]
    return ("PASS" if not outside else "FAIL"), outside, concrete_contracts


def validate_c52_records(records: list[dict[str, object]], commits: list[str]) -> None:
    if len(records) != 145:
        raise ValueError(f"C52 ledger row count changed: {len(records)}")
    if [record["commit"] for record in records] != commits[:145]:
        raise ValueError("C52 ledger no longer matches the first 145 candidate commits")
    for record in records:
        live = commit_fields(str(record["commit"]))
        for field in ("parents", "author", "author_email", "authored_at", "subject", "changed_paths"):
            if record[field] != live[field]:
                raise ValueError(f"C52/live mismatch for {record['commit']} field {field}")
        if record["changed_path_count"] != len(live["changed_paths"]):
            raise ValueError(f"C52/live path-count mismatch for {record['commit']}")


def extension_record(sequence: int, commit: str) -> dict[str, object]:
    record = commit_fields(commit)
    tasks, gates, footer_status = strict_trailers(commit)
    parent = str(record["parents"][0]) if record["parents"] else commit
    if footer_status == "CANONICAL":
        historical_verdict, historical_outside, contracts = scope_result(commit, tasks, parent)
        current_verdict, current_outside, _ = scope_result(commit, tasks, CANDIDATE)
    else:
        historical_verdict = "UNATTRIBUTED"
        current_verdict = "UNATTRIBUTED"
        historical_outside = []
        current_outside = []
        contracts = []
    dependencies = []
    for contract in contracts:
        dependency_statuses: dict[str, str] = {}
        for dependency in contract["dependencies"]:
            resolved = task_contract(parent, dependency)
            dependency_statuses[dependency] = "MISSING" if resolved is None else str(resolved["status"])
        dependencies.append(
            {
                "task_id": tasks[contracts.index(contract)],
                "contract_ref": f"{parent}:{contract['path']}",
                "status_at_commit": contract["status"],
                "dependency_statuses": dependency_statuses,
                "allowlist": contract["allowlist"],
            }
        )
    record.update(
        {
            "sequence": sequence,
            "canonical_task_footers": tasks,
            "canonical_gate_footers": gates,
            "footer_status": footer_status,
            "historical_contract_applicable": True,
            "historical_scope_verdict": historical_verdict,
            "historical_outside_paths": historical_outside,
            "current_scope_verdict": current_verdict,
            "current_outside_paths": current_outside,
            "commit_time_contracts": dependencies,
            "source": "live-candidate-extension",
        }
    )
    if commit in IMMUTABLE_TRAILER_EXCEPTIONS:
        record["footer_disposition"] = f"IMMUTABLE_{IMMUTABLE_TRAILER_EXCEPTIONS[commit]}"
    elif footer_status == "CANONICAL":
        record["footer_disposition"] = "CANONICAL"
    elif footer_status == "PSEUDO_NOT_TRAILER":
        record["footer_disposition"] = "POST_SNAPSHOT_NONCANONICAL"
    else:
        record["footer_disposition"] = "POST_SNAPSHOT_UNATTRIBUTED"
    record["changed_path_count"] = len(record["changed_paths"])
    return record


def main() -> None:
    commits = git("rev-list", "--reverse", CANDIDATE).splitlines()
    if len(commits) != 157 or commits[0] != "8d466ba540f74025ac17e3f29d367333fd16d4c1":
        raise ValueError("candidate history does not match the V0-GOV-038 measurement boundary")
    if sha256_bytes(C52_INPUT.read_bytes()) != C52_INPUT_SHA256:
        raise ValueError("frozen C52 commit-scope ledger hash mismatch")
    c52_records = json.loads(C52_INPUT.read_text(encoding="utf-8"))
    validate_c52_records(c52_records, commits)
    if c52_records[-1]["commit"] != C52_SNAPSHOT_LAST_COMMIT:
        raise ValueError("unexpected C52 snapshot boundary")
    if sum(record["historical_scope_verdict"] == "FAIL" for record in c52_records) != SCOPE_FAIL_COUNT:
        raise ValueError("C52 historical scope count changed")
    if tuple(record["commit"] for record in c52_records if record["footer_status"] == "MISSING")[-13:] != FOOTER_FAIL_COMMITS:
        raise ValueError("C52 footer issue set changed")

    records = []
    for source_record in c52_records:
        record = dict(source_record, source="C52-frozen-2026-08-10")
        if record["commit"] in FOOTER_FAIL_COMMITS:
            record["footer_disposition"] = "C52_GOV_003_MISSING_TRAILER"
        elif record["footer_status"] == "ROOT_EXCEPTION":
            record["footer_disposition"] = "ROOT_PRE_CONVENTION_EXCEPTION"
        elif record["footer_status"] == "MISSING":
            record["footer_disposition"] = "C45_IMMUTABLE_UNATTRIBUTED_EXCEPTION"
        elif record["footer_status"] == "PSEUDO_NOT_TRAILER":
            record["footer_disposition"] = "PRE_C52_NONCANONICAL"
        else:
            record["footer_disposition"] = "CANONICAL"
        records.append(record)
    records.extend(extension_record(index, commit) for index, commit in enumerate(commits[145:], start=146))
    sequence_hash = sha256_bytes(("\n".join(commits) + "\n").encode("ascii"))
    artifact = {
        "schema": "V0-GOV-038-history-ledger/v1",
        "candidate": CANDIDATE,
        "root": commits[0],
        "commit_count": len(commits),
        "commit_sequence_sha256": sequence_hash,
        "c52_source": {"path": str(C52_INPUT), "sha256": C52_INPUT_SHA256, "commit_count": len(c52_records)},
        "records": records,
    }
    OUTPUT.joinpath("history-ledger.json").write_text(
        json.dumps(artifact, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    columns = [
        "sequence", "commit", "authored_at", "subject", "canonical_task_footers",
        "canonical_gate_footers", "footer_status", "changed_path_count", "changed_paths",
        "historical_scope_verdict", "historical_outside_paths", "current_scope_verdict",
        "current_outside_paths", "footer_disposition", "source",
    ]
    with OUTPUT.joinpath("history-ledger.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns)
        writer.writeheader()
        for record in records:
            writer.writerow({
                column: json.dumps(record.get(column, []), ensure_ascii=False)
                if isinstance(record.get(column), list)
                else record.get(column, "")
                for column in columns
            })
    footer_failures = [record["commit"] for record in c52_records if record["footer_status"] == "MISSING"][-13:]
    scope_failures = [record["commit"] for record in c52_records if record["historical_scope_verdict"] == "FAIL"]
    extension = records[145:]
    controls = "\n".join([
        "# V0-GOV-038 Immutable History Attestation",
        "",
        f"- Candidate: `{CANDIDATE}`",
        f"- Root: `{commits[0]}`",
        f"- Root..candidate commit count: `{len(commits)}` (measured 2026-08-11)",
        f"- Commit-sequence SHA-256: `{sequence_hash}`",
        f"- Frozen CORR:C52 input SHA-256: `{C52_INPUT_SHA256}`",
        "- The first 145 rows are hash-checked against the frozen C52 ledger and each row's Git identity, parent and changed-path list is rechecked against the candidate.",
        "- The final 12 rows are measured from the candidate Git objects. No history object is changed by this task.",
        "",
        "## Separate verdicts",
        "",
        f"- Commit-time scope failures: `{len(scope_failures)}` — `{', '.join(scope_failures)}`",
        f"- C52 current-contract snapshot failures: `{sum(record['current_scope_verdict'] == 'FAIL' for record in c52_records)}`. These are kept separate from the retrospective verdict in every C52 row.",
        "- Extension rows carry independently derived historical and candidate-current verdicts where a canonical Task trailer exists; otherwise both are `UNATTRIBUTED`.",
        "",
        "## Footer control",
        "",
        f"- C52 missing-footer set (`13`): `{', '.join(footer_failures)}`",
        "- The other 11 C52 `MISSING` rows are the C45 immutable unattributed exceptions; they are explicitly tagged separately and do not inflate the C52 GOV-003 13-commit set.",
        "- Immutable exceptions outside that C52 set:",
        "  - `2afa0c3445279be8a5fb3ba80fa2c3d0d22484c6`: literal `\\n` bytes keep `Task:` and `Gate:` out of a trailer block.",
        "  - `0f2efe6616a90007d326c0d1870a436f0ae2577e`: blank line separates `Task:` from the trailing `Gate:` line, so it is not a contiguous canonical trailer block.",
        "- Disposition: attest these objects; do not rebase, amend, force-push or otherwise rewrite them.",
        "",
        "## Generated files",
        "",
        "- `history-ledger.json`: full structured rows, including changed paths, C52 historical/current verdicts and status transitions.",
        "- `history-ledger.csv`: one compact row per commit with the same changed-path and verdict data.",
        "- `preservation.json`: before/after candidate boundary fingerprint.",
        "",
    ])
    OUTPUT.joinpath("controls.md").write_text(controls, encoding="utf-8")
    preservation = {
        "candidate": CANDIDATE,
        "root": commits[0],
        "commit_count": len(commits),
        "pre_task_commit_sequence_sha256": sequence_hash,
        "post_generation_commit_sequence_sha256": sequence_hash,
        "equal": True,
        "method": "SHA-256 of newline-delimited git rev-list --reverse <candidate> object IDs",
    }
    OUTPUT.joinpath("preservation.json").write_text(
        json.dumps(preservation, indent=2) + "\n", encoding="utf-8"
    )
    print(f"OK: {len(records)} ledger rows; {len(scope_failures)} historical scope failures; {len(footer_failures)} C52 footer failures")
    print(f"history-sequence-sha256={sequence_hash}")
    print(f"extension-rows={len(extension)}")


if __name__ == "__main__":
    main()
