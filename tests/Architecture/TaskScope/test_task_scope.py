from __future__ import annotations

"""Tests for the task-scope enforcement tool.

Covers: allow/deny, dirty-worktree, untracked, delete, rename, path
traversal, Windows normalization, missing/multiple task IDs, incomplete
dependency, wrong status/assignee, broken Markdown, evidence directory,
metadata file, glob matching, and deterministic output.
"""

import copy
import csv
import json
import re
import subprocess
from pathlib import Path

import pytest


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        text=True,
        check=True,
    ).stdout


def _write(repo: Path, rel: str, content: str = "x\n") -> Path:
    """Write a file inside the repo and return its path."""
    p = repo / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8")
    return p


def _stage(repo: Path, rel: str, content: str = "x\n") -> Path:
    """Write and stage a file."""
    p = _write(repo, rel, content)
    _git(repo, "add", rel)
    return p


def _commit(repo: Path, rel: str, content: str = "x\n") -> Path:
    """Write, stage, and commit a file."""
    p = _stage(repo, rel, content)
    _git(repo, "commit", "-q", "-m", f"add {rel}")
    return p


def _write_remediation_exceptions(plan_dir: Path, rows: list[str]) -> None:
    """Write the strict GATES.md remediation-exception table fixture."""
    table = "\n".join(rows)
    (plan_dir / "GATES.md").write_text(
        "\n".join(
            [
                "# Version Gates",
                "",
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->",
                "| Task ID | Approval date | Source basis | Purpose | Gate closure evidence | New feature behavior |",
                "| --- | --- | --- | --- | --- | --- |",
                table,
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->",
                "",
            ]
        ),
        encoding="utf-8",
    )


def _write_v0_deferrals(plan_dir: Path, rows: list[str]) -> None:
    """Write the strict GATES.md V0-deferral table fixture."""
    table = "\n".join(rows)
    (plan_dir / "GATES.md").write_text(
        "\n".join(
            [
                "# Version Gates",
                "",
                "<!-- V0_DEFERRED_TASKS:START -->",
                "| Task ID | Approval date | Reopen stage | Required evidence | Gate closure evidence |",
                "| --- | --- | --- | --- | --- |",
                table,
                "<!-- V0_DEFERRED_TASKS:END -->",
                "",
            ]
        ),
        encoding="utf-8",
    )


REMEDIATION_RECORDS = {
    "V1-CAT-003": ("2026-08-10", "CORR:C52"),
    "V1-FND-016": ("2026-08-10", "CORR:C52"),
    "V1-FND-017": ("2026-08-10", "CORR:C52"),
    "V1-FND-018": ("2026-08-10", "CORR:C52"),
    "V1-FND-019": ("2026-08-10", "CORR:C52"),
    "V1-FND-020": ("2026-08-10", "CORR:C52"),
    "V1-FND-021": ("2026-08-10", "CORR:C52"),
    "V1-FND-022": ("2026-08-10", "CORR:C52"),
    "V1-FND-023": ("2026-08-11", "CORR:C52;CORR:C53;CORR:C54"),
    "V1-IAM-006": ("2026-08-10", "CORR:C52"),
    "V1-IAM-007": ("2026-08-10", "CORR:C52"),
    "V1-IAM-008": ("2026-08-10", "CORR:C52"),
    "V1-IAM-009": ("2026-08-10", "CORR:C52"),
    "V1-IAM-010": ("2026-08-10", "CORR:C52"),
    "V1-IAM-011": ("2026-08-10", "CORR:C52"),
    "V1-IAM-012": ("2026-08-10", "CORR:C52"),
    "V1-IAM-013": ("2026-08-10", "CORR:C52"),
    "V1-SEC-004": ("2026-08-10", "CORR:C52"),
    "V1-SEC-005": ("2026-08-10", "CORR:C52"),
}
REMEDIATION_ROWS = [
    "| `{task_id}` | `{approval_date}` | `{source_basis}` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |".format(
        task_id=task_id, approval_date=approval_date, source_basis=source_basis
    )
    for task_id, (approval_date, source_basis) in REMEDIATION_RECORDS.items()
]
C52_C53_C54_REMEDIATION_TASK_IDS = list(REMEDIATION_RECORDS)
FND023_SOURCE_BASIS = [
    "- CORR:C52",
    "- CORR:C53",
    "- CORR:C54",
    "- CORR:C57",
]

ORIGIN_FINDING_IDS = [
    "GOV-001",
    "GOV-002",
    "GOV-006",
    "CODE-001",
    "CODE-002",
    "CODE-004",
    "CODE-006",
    "CODE-013",
    "CODE-016",
    "GOV-003",
    "GOV-004",
    "GOV-007",
    "GOV-008",
    "GOV-010",
    "GOV-012",
    "VER-CI-001",
    "VER-GOV-002",
    "VER-GOV-003",
    "CODE-003",
    "CODE-005",
    "CODE-007",
    "CODE-008",
    "CODE-009",
    "CODE-010",
    "CODE-011",
    "CODE-012",
    "CODE-014",
    "CODE-015",
    "CODE-018",
    "CODE-019",
    "GOV-005",
    "GOV-009",
    "GOV-011",
    "GOV-013",
    "GOV-014",
    "GOV-015",
    "VER-BUILD-004",
    "VER-COV-007",
    "VER-FMT-005",
    "VER-PROV-006",
    "CODE-017",
    "VER-ACC-008",
]

CANONICAL_POST_CLOSURE_ROUTES = {
    "POST-CL-010": {
        "owner_task_ids": "V0-GOV-056",
        "source_basis": "CORR:C59",
        "prerequisites": "V0-GOV-050;V0-GOV-054",
        "closure_evidence": "exact C52/C53/C54/C57 source-basis and 42+10 routing/catalog parity",
    },
    "POST-CL-011": {
        "owner_task_ids": "V0-GOV-057",
        "source_basis": "CORR:C60",
        "prerequisites": "V0-GOV-055",
        "closure_evidence": "V1-FND-023 Done admission invokes the fixed task-specific v3 verifier regardless of V0 gate state",
    },
}

REQUIRED_ROUTE_CATALOG_ENTRIES = {
    "V0-GOV-056": {
        "task_path": "plan/v0/governance/V0-GOV-056-task-scope-parity-regression.md",
        "dependencies": ["V0-GOV-050", "V0-GOV-054"],
        "closure_evidence": "exact C52/C53/C54/C57 TaskScope source basis and 42+10 routing/catalog parity",
    },
    "V0-GOV-057": {
        "task_path": "plan/v0/governance/V0-GOV-057-v3-admission-hardening.md",
        "dependencies": ["V0-GOV-055"],
        "closure_evidence": "V1-FND-023 Done admission invokes the fixed task-specific v3 verifier regardless of V0 gate state",
    },
    "V0-GOV-058": {
        "task_path": "plan/v0/governance/V0-GOV-058-dynamic-route-parity.md",
        "dependencies": ["V0-GOV-056", "V0-GOV-057"],
        "closure_evidence": "immutable 42-ID baseline plus dynamic post-closure count and C59/C60 CSV/JSON/catalog parity",
    },
}

POST_CLOSURE_ID_PATTERN = re.compile(r"POST-CL-\d{3}$")


def _load_live_route_data(repository: Path) -> tuple[list[dict], dict]:
    with (repository / "plan" / "AUDIT_REMEDIATION_ROUTING.csv").open(
        encoding="utf-8", newline=""
    ) as handle:
        csv_items = list(csv.DictReader(handle))
    routing = json.loads(
        (repository / "plan" / "AUDIT_REMEDIATION_ROUTING.json").read_text(
            encoding="utf-8"
        )
    )
    return csv_items, routing


def _assert_unique_and_canonical_order(csv_ids: list[str], json_ids: list[str]) -> None:
    assert len(csv_ids) == len(set(csv_ids))
    assert len(json_ids) == len(set(json_ids))
    assert csv_ids == json_ids


def _assert_field_parity(csv_item: dict, json_item: dict) -> None:
    assert set(json_item) <= set(csv_item)
    for key in json_item:
        if key in ("owner_task_ids", "prerequisites"):
            assert csv_item[key] == ";".join(json_item[key])
        else:
            assert str(csv_item[key]) == str(json_item[key])


def _assert_live_route_invariants(csv_items: list[dict], routing: dict) -> None:
    csv_ids = [row["finding_id"] for row in csv_items]
    json_ids = [item["finding_id"] for item in routing["items"]]
    _assert_unique_and_canonical_order(csv_ids, json_ids)
    origin_ids = [row["finding_id"] for row in csv_items if not row["finding_id"].startswith("POST-CL-")]
    assert origin_ids == ORIGIN_FINDING_IDS
    post_closure_rows = [
        row for row in csv_items if row["finding_id"].startswith("POST-CL-")
    ]
    assert post_closure_rows == sorted(post_closure_rows, key=lambda row: row["finding_id"])
    assert all(POST_CLOSURE_ID_PATTERN.match(row["finding_id"]) for row in post_closure_rows)
    assert len(origin_ids) == 42
    assert len(csv_items) == 42 + len(post_closure_rows)
    assert routing["audit_register"]["finding_count"] == 42
    assert routing["audit_register"]["post_closure_finding_count"] == len(post_closure_rows)
    assert routing["audit_register"]["routed_finding_count"] == len(csv_items)
    for csv_item, json_item in zip(csv_items, routing["items"]):
        _assert_field_parity(csv_item, json_item)
    csv_by_id = {row["finding_id"]: row for row in csv_items}
    json_by_id = {item["finding_id"]: item for item in routing["items"]}
    for post_id, expected in CANONICAL_POST_CLOSURE_ROUTES.items():
        assert post_id in csv_by_id
        assert post_id in json_by_id
        for key, value in expected.items():
            assert csv_by_id[post_id][key] == value
            if key in ("owner_task_ids", "prerequisites"):
                assert json_by_id[post_id][key] == value.split(";")
            else:
                assert json_by_id[post_id][key] == value
    catalog_by_id = {item["task_id"]: item for item in routing["task_catalog"]}
    for task_id, expected in REQUIRED_ROUTE_CATALOG_ENTRIES.items():
        assert task_id in catalog_by_id
        for key, value in expected.items():
            assert catalog_by_id[task_id][key] == value


def _mutate_drop_post_closure_from_csv(csv_items: list[dict], routing: dict) -> None:
    csv_items[:] = [row for row in csv_items if row["finding_id"] != "POST-CL-015"]


def _mutate_malformed_post_closure_id(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-015":
            row["finding_id"] = "POST-CL-15"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-015":
            item["finding_id"] = "POST-CL-15"


def _mutate_non_canonical_post_closure_id(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-015":
            row["finding_id"] = "POST-CL-015A"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-015":
            item["finding_id"] = "POST-CL-015A"


def _mutate_extra_post_closure_row(csv_items: list[dict], routing: dict) -> None:
    csv_items.append(dict(csv_items[-1], finding_id="POST-CL-016"))


def _mutate_duplicate_post_closure_row(csv_items: list[dict], routing: dict) -> None:
    csv_items.append(dict(csv_items[-1]))
    routing["items"].append(dict(routing["items"][-1]))


def _mutate_missing_origin_id(csv_items: list[dict], routing: dict) -> None:
    csv_items[:] = [row for row in csv_items if row["finding_id"] != "GOV-001"]
    routing["items"][:] = [
        item for item in routing["items"] if item["finding_id"] != "GOV-001"
    ]


def _mutate_extra_origin_id(csv_items: list[dict], routing: dict) -> None:
    csv_items.append(dict(csv_items[0], finding_id="GOV-999"))
    routing["items"].append(dict(routing["items"][0], finding_id="GOV-999"))


def _mutate_duplicate_origin_id(csv_items: list[dict], routing: dict) -> None:
    csv_items.append(dict(csv_items[0]))
    routing["items"].append(dict(routing["items"][0]))


def _mutate_csv_json_row_mismatch(csv_items: list[dict], routing: dict) -> None:
    csv_items[:] = [row for row in csv_items if row["finding_id"] != "POST-CL-013"]


def _mutate_register_count_mismatch(csv_items: list[dict], routing: dict) -> None:
    routing["audit_register"]["post_closure_finding_count"] = 14


def _mutate_field_mismatch(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-010":
            row["closure_evidence"] = "mutated closure evidence"


def _mutate_catalog_mismatch(csv_items: list[dict], routing: dict) -> None:
    for entry in routing["task_catalog"]:
        if entry["task_id"] == "V0-GOV-058":
            entry["dependencies"] = ["V0-GOV-056"]


def _mutate_c59_owner_difference(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-010":
            row["owner_task_ids"] = "V0-GOV-999"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-010":
            item["owner_task_ids"] = ["V0-GOV-999"]


def _mutate_c59_source_difference(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-010":
            row["source_basis"] = "CORR:XX"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-010":
            item["source_basis"] = "CORR:XX"


def _mutate_c59_prerequisite_difference(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-010":
            row["prerequisites"] = "V0-GOV-000"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-010":
            item["prerequisites"] = ["V0-GOV-000"]


def _mutate_c60_closure_evidence_difference(csv_items: list[dict], routing: dict) -> None:
    for row in csv_items:
        if row["finding_id"] == "POST-CL-011":
            row["closure_evidence"] = "mutated C60 closure"
    for item in routing["items"]:
        if item["finding_id"] == "POST-CL-011":
            item["closure_evidence"] = "mutated C60 closure"

DEFERRED_TASK_IDS = [
    "V0-HUG-001",
    "V0-QNB-001",
    "V0-YSP-001",
    "V0-MCD-001",
    "V0-PRN-001",
    "V0-QRG-001",
    "V0-CMP-001",
    "V0-SEC-001",
    "V0-LIC-001",
    "V0-BKP-001",
    "V0-BKP-002",
]

# 2026-08-15 (TRACEABILITY C69): V0-REV-001..030 (Done, phase 1.1) and
# V0-GOV-041/042 (NotApplicable, phase 1.2) were removed from the deferral
# list in GATES.md; only the 2026-08-03 C40 entries remain.

DEFERRED_ROWS = [
    "| `V0-HUG-001` | `2026-08-03` | `V12` | Gerçek Hugin provider contract/erişim kanıtı | Not V0 gate closure evidence |",
    "| `V0-QNB-001` | `2026-08-03` | `V13` | Gerçek QNB provider contract/erişim kanıtı | Not V0 gate closure evidence |",
    "| `V0-YSP-001` | `2026-08-03` | `V12` | Gerçek Yapı Kredi provider contract/erişim kanıtı | Not V0 gate closure evidence |",
    "| `V0-MCD-001` | `2026-08-03` | `V12` | Gerçek meal-card provider sözleşme/onay kanıtı | Not V0 gate closure evidence |",
    "| `V0-PRN-001` | `2026-08-03` | `V14` | Gerçek yazıcı/cihaz sözleşmesi veya onay kanıtı | Not V0 gate closure evidence |",
    "| `V0-QRG-001` | `2026-08-03` | `V14` | Gerçek QR relay public kanal onay kanıtı | Not V0 gate closure evidence |",
    "| `V0-CMP-001` | `2026-08-03` | `V12` | Mali müşavir onaylı FSC/T300-QNB adisyon strateji kararı | Not V0 gate closure evidence |",
    "| `V0-SEC-001` | `2026-08-03` | `V14` | Doğrulanmış güvenlik gereksinim kaynağı/standart kanıtı | Not V0 gate closure evidence |",
    "| `V0-LIC-001` | `2026-08-03` | `V20` | Gerçek license server ve lisans sözleşmesi kanıtı | Not V0 gate closure evidence |",
    "| `V0-BKP-001` | `2026-08-03` | `V15` | Gerçek PostgreSQL 18 ikinci instance/cihaz kanıtı | Not V0 gate closure evidence |",
    "| `V0-BKP-002` | `2026-08-03` | `V15` | Gerçek yedekleme donanımı/cihaz kanıtı | Not V0 gate closure evidence |",
]


# ---------------------------------------------------------------------------
# Allow / deny
# ---------------------------------------------------------------------------

class TestAllowDeny:
    def test_empty_worktree_valid(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
        assert result["valid"] is True
        assert result["findings"] == []

    def test_valid_changes_within_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "tools/task-scope/new_file.py")
        _write(make_repo, "tests/architecture/taskscope/new_test.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
        assert result["valid"] is True

    def test_changes_outside_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "src/other_module/file.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert result["valid"] is False
        paths = [f["path"] for f in result["findings"]]
        assert "src/other_module/file.py" in paths

    def test_another_task_file_rejected(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "plan/v1/foundation/V1-FND-004-other.md")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert any("v1-fnd-004" in p for p in paths)


# ---------------------------------------------------------------------------
# Untracked
# ---------------------------------------------------------------------------

class TestUntracked:
    def test_untracked_outside_scope_caught(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "random/untracked.txt")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "random/untracked.txt" in paths

    def test_untracked_within_scope_allowed(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "tools/task-scope/new_untracked.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Delete
# ---------------------------------------------------------------------------

class TestDelete:
    def test_deleted_outside_scope_caught(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _commit(make_repo, "src/existing.py", "old\n")
        (make_repo / "src/existing.py").unlink()
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "src/existing.py" in paths

    def test_deleted_within_scope_allowed(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _commit(make_repo, "tools/task-scope/old.py", "old\n")
        (make_repo / "tools/task-scope/old.py").unlink()
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Rename
# ---------------------------------------------------------------------------

class TestRename:
    def test_rename_both_paths_in_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _commit(make_repo, "tools/task-scope/old_name.py", "content\n")
        _git(make_repo, "mv", "tools/task-scope/old_name.py", "tools/task-scope/new_name.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_rename_only_new_path_in_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _commit(make_repo, "src/old_name.py", "content\n")
        (make_repo / "tools/task-scope").mkdir(parents=True, exist_ok=True)
        _git(make_repo, "mv", "src/old_name.py", "tools/task-scope/new_name.py")
        _git(make_repo, "add", "-A")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert any("src/old_name.py" in p for p in paths)

    def test_rename_only_old_path_in_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _commit(make_repo, "tools/task-scope/old_name.py", "content\n")
        (make_repo / "src").mkdir(parents=True, exist_ok=True)
        _git(make_repo, "mv", "tools/task-scope/old_name.py", "src/new_name.py")
        _git(make_repo, "add", "-A")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert any("src/new_name.py" in p for p in paths)


# ---------------------------------------------------------------------------
# Path traversal
# ---------------------------------------------------------------------------

class TestPathTraversal:
    def test_traversal_in_finding_path(self, write_task, make_repo, make_plan, run_tool):
        """A path containing .. is flagged as traversal even if it would match."""
        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)

        assert mod.contains_traversal("../etc/passwd") is True
        assert mod.contains_traversal("tools/../etc/passwd") is True
        assert mod.contains_traversal("tools/task-scope/file.py") is False
        assert mod.contains_traversal("tools/task-scope/../other.py") is True

        change = mod.GitChange(
            path="tools/task-scope/../etc/passwd",
            change_type="untracked",
        )
        findings = mod.validate_changes([change], ["tools/task-scope/**"])
        assert len(findings) == 1
        assert findings[0]["reason"] == "path traversal detected"


# ---------------------------------------------------------------------------
# Windows path normalization
# ---------------------------------------------------------------------------

class TestWindowsNormalization:
    def test_backslash_paths_normalized(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _write(make_repo, "tools/task-scope/sub/file.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_case_insensitive_match(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _write(make_repo, "Tools/Task-Scope/file.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Task metadata validation
# ---------------------------------------------------------------------------

class TestTaskMetadata:
    def test_missing_task_file(self, make_repo, make_plan, run_tool):
        exit_code, result = run_tool("V1-FND-999", make_repo, make_plan)
        assert exit_code == 1
        assert len(result["metadata_errors"]) > 0

    def test_multiple_task_ids(self, write_task, make_repo, make_plan, run_tool):
        task_file = write_task()
        text = task_file.read_text(encoding="utf-8")
        text += "- Task ID: V1-FND-003\n"
        task_file.write_text(text, encoding="utf-8")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("exactly one Task ID" in e for e in result["metadata_errors"])

    def test_done_status_rejected(self, write_task, make_repo, make_plan, run_tool):
        write_task(status="Done")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Done" in e for e in result["metadata_errors"])

    def test_wrong_assignee_unassigned(self, write_task, make_repo, make_plan, run_tool):
        write_task(assignee="Unassigned (exactly one person)")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Assignee" in e for e in result["metadata_errors"])

    def test_generic_assignee_agent(self, write_task, make_repo, make_plan, run_tool):
        write_task(assignee="Agent")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Assignee" in e for e in result["metadata_errors"])

    def test_generic_assignee_ai(self, write_task, make_repo, make_plan, run_tool):
        write_task(assignee="AI")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Assignee" in e for e in result["metadata_errors"])


# ---------------------------------------------------------------------------
# Dependency validation
# ---------------------------------------------------------------------------

class TestDependencies:
    def test_incomplete_dependency(self, write_task, make_repo, make_plan, run_tool):
        write_task(dependencies="- V1-FND-001")
        write_task(
            task_id="V1-FND-001",
            status="InProgress",
            owned_surface="- `other/**`",
        )
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("V1-FND-001" in e for e in result["metadata_errors"])

    def test_complete_dependency(self, write_task, write_done_dep, make_repo, make_plan, run_tool):
        write_task(dependencies="- V1-FND-001")
        dep_file = write_done_dep("V1-FND-001")
        _git(make_repo, "add", str(dep_file.relative_to(make_repo)))
        _git(make_repo, "commit", "-q", "-m", "add dependency")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_missing_dependency_file(self, write_task, make_repo, make_plan, run_tool):
        write_task(dependencies="- V1-FND-999")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("V1-FND-999" in e for e in result["metadata_errors"])


# ---------------------------------------------------------------------------
# User-approved remediation entry-gate exceptions
# ---------------------------------------------------------------------------

class TestRemediationEntryGateExceptions:
    def _prepare_open_v0_gate(self, write_task, make_repo, make_plan) -> None:
        _write_remediation_exceptions(make_plan, REMEDIATION_ROWS)
        write_task(task_id="V0-DOM-001", status="Planned")

    @pytest.mark.parametrize("task_id", C52_C53_C54_REMEDIATION_TASK_IDS)
    def test_every_c52_c53_c54_task_bypasses_open_v0_entry_gate(
        self, task_id, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id=task_id)

        exit_code, result = run_tool(task_id, make_repo, make_plan)

        assert exit_code == 0
        assert result["metadata_errors"] == []

    def test_exception_records_require_the_canonical_metadata(
        self, make_plan
    ):
        _write_remediation_exceptions(make_plan, REMEDIATION_ROWS)

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)

        assert mod.parse_remediation_exception_records(make_plan) == REMEDIATION_RECORDS

    def test_fnd023_wrong_approval_date_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-023")
        wrong_date_rows = REMEDIATION_ROWS.copy()
        wrong_date_rows[8] = wrong_date_rows[8].replace("2026-08-11", "2026-08-10")
        _write_remediation_exceptions(make_plan, wrong_date_rows)

        exit_code, result = run_tool("V1-FND-023", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    def test_fnd023_wrong_source_basis_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-023")
        wrong_source_rows = REMEDIATION_ROWS.copy()
        wrong_source_rows[8] = wrong_source_rows[8].replace(
            "CORR:C52;CORR:C53;CORR:C54", "CORR:C52"
        )
        _write_remediation_exceptions(make_plan, wrong_source_rows)

        exit_code, result = run_tool("V1-FND-023", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    def test_out_of_order_exception_markers_fail_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-023")
        gates = make_plan / "GATES.md"
        gates.write_text(
            gates.read_text(encoding="utf-8").replace(
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->",
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:TEMP -->",
            ).replace(
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->",
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->",
            ).replace(
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:TEMP -->",
                "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->",
            ),
            encoding="utf-8",
        )

        exit_code, result = run_tool("V1-FND-023", make_repo, make_plan)

        assert exit_code == 1
        assert any("markers are out of order" in error for error in result["metadata_errors"])

    def test_candidate_remediation_skips_only_blocked_dependencies(
        self, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016", dependencies="- V0-DOM-001")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            "V1-FND-016", make_repo, make_plan, candidate_remediation=True
        )

        assert result["valid"] is True

    def test_candidate_remediation_rejects_unapproved_task(
        self, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-999", dependencies="- V0-DOM-001")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            "V1-FND-999", make_repo, make_plan, candidate_remediation=True
        )

        assert result["valid"] is False
        assert "not an approved" in result["metadata_errors"][0]

    def test_unapproved_task_cannot_bypass_open_v0_entry_gate(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-999")

        exit_code, result = run_tool("V1-FND-999", make_repo, make_plan)

        assert exit_code == 1
        assert any("GATE-V0-EXIT" in error for error in result["metadata_errors"])

    def test_duplicate_exception_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        _write_remediation_exceptions(make_plan, REMEDIATION_ROWS + [REMEDIATION_ROWS[0]])

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("duplicate Task ID" in error for error in result["metadata_errors"])

    def test_nonmatching_exception_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        nonmatching_rows = REMEDIATION_ROWS[:-1] + [
            "| `V1-FND-999` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |"
        ]
        _write_remediation_exceptions(make_plan, nonmatching_rows)

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    def test_malformed_exception_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        malformed_rows = REMEDIATION_ROWS.copy()
        malformed_rows[0] = "| `V1-FND-016` | malformed |"
        _write_remediation_exceptions(make_plan, malformed_rows)

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("invalid record" in error for error in result["metadata_errors"])

    def test_missing_exception_markers_fail_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        (make_plan / "GATES.md").write_text("# Version Gates\n", encoding="utf-8")

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("markers must occur exactly once" in error for error in result["metadata_errors"])

    def test_wrong_c52_approval_date_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        wrong_date_rows = REMEDIATION_ROWS.copy()
        wrong_date_rows[0] = wrong_date_rows[0].replace("2026-08-10", "2026-08-09")
        _write_remediation_exceptions(make_plan, wrong_date_rows)

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    @pytest.mark.parametrize("task_id", ["V1-FND-001", "V0-GOV-052"])
    def test_existing_done_task_is_not_a_candidate_remediation(
        self, task_id, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id=task_id, status="Done")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            task_id, make_repo, make_plan, candidate_remediation=True
        )

        assert result["valid"] is False
        assert result["metadata_errors"] == [
            f"Task {task_id} is not an approved candidate-code remediation"
        ]

    def test_c52_candidate_requires_an_active_session(
        self, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016", status="Planned")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            "V1-FND-016", make_repo, make_plan, candidate_remediation=True
        )

        assert result["valid"] is False
        assert result["metadata_errors"] == [
            "Candidate-code remediation task status is 'Planned', expected 'InProgress'"
        ]

    def test_fnd023_candidate_requires_an_active_session(
        self, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-023", status="Planned")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            "V1-FND-023", make_repo, make_plan, candidate_remediation=True
        )

        assert result["valid"] is False
        assert result["metadata_errors"] == [
            "Candidate-code remediation task status is 'Planned', expected 'InProgress'"
        ]

    def test_non_c52_source_basis_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-016")
        wrong_source_rows = REMEDIATION_ROWS.copy()
        wrong_source_rows[0] = wrong_source_rows[0].replace("CORR:C52", "PDF:I.7")
        _write_remediation_exceptions(make_plan, wrong_source_rows)

        exit_code, result = run_tool("V1-FND-016", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    @staticmethod
    def _assert_fnd023_source_basis(source_basis: list[str]) -> None:
        assert source_basis == FND023_SOURCE_BASIS

    @pytest.mark.parametrize(
        "source_basis",
        [
            FND023_SOURCE_BASIS[:-1],
            FND023_SOURCE_BASIS + ["- CORR:C58"],
            [*FND023_SOURCE_BASIS[:2], FND023_SOURCE_BASIS[3], FND023_SOURCE_BASIS[2]],
        ],
        ids=["missing", "extra", "out_of_order"],
    )
    def test_fnd023_source_basis_negative_variants_fail_closed(self, source_basis):
        with pytest.raises(AssertionError):
            self._assert_fnd023_source_basis(source_basis)

    def test_repository_fnd023_admission_source_and_routing_catalog_parity(self):
        repository = Path(__file__).resolve().parents[3]
        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            repository / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        fnd023 = (repository / "plan" / "v1" / "foundation" / "V1-FND-023-solution-test-discovery.md").read_text(
            encoding="utf-8"
        )
        source_basis = fnd023.split("## Source basis\n\n", 1)[1].split("\n## ", 1)[0]
        self._assert_fnd023_source_basis(source_basis.splitlines())

        csv_items, routing = _load_live_route_data(repository)
        csv_item = next(item for item in csv_items if item["finding_id"] == "POST-CL-002")
        json_item = next(item for item in routing["items"] if item["finding_id"] == "POST-CL-002")
        v050_catalog = next(
            item for item in routing["task_catalog"] if item["task_id"] == "V0-GOV-050"
        )

        assert mod.parse_remediation_exception_records(repository / "plan") == REMEDIATION_RECORDS
        assert len(REMEDIATION_RECORDS) == 19
        _assert_live_route_invariants(csv_items, routing)
        assert csv_item["owner_task_ids"] == "V0-GOV-050;V1-FND-023"
        assert csv_item["source_basis"] == "CORR:C52;CORR:C53;CORR:C54"
        assert csv_item["closure_evidence"] == json_item["closure_evidence"]
        assert json_item["owner_task_ids"] == ["V0-GOV-050", "V1-FND-023"]
        assert json_item["source_basis"] == "CORR:C52;CORR:C53;CORR:C54"
        assert v050_catalog["task_path"] == "plan/v0/governance/V0-GOV-050-admit-post-closure-remediation.md"
        assert v050_catalog["dependencies"] == ["V0-GOV-035", "V0-GOV-037", "V0-GOV-049", "V0-GOV-052"]
        assert v050_catalog["closure_evidence"] == "exact 19-ID C52/C53/C54 admission set"

    @pytest.mark.parametrize(
        "mutate",
        [
            _mutate_drop_post_closure_from_csv,
            _mutate_malformed_post_closure_id,
            _mutate_non_canonical_post_closure_id,
            _mutate_extra_post_closure_row,
            _mutate_duplicate_post_closure_row,
            _mutate_missing_origin_id,
            _mutate_extra_origin_id,
            _mutate_duplicate_origin_id,
            _mutate_csv_json_row_mismatch,
            _mutate_register_count_mismatch,
            _mutate_field_mismatch,
            _mutate_catalog_mismatch,
            _mutate_c59_owner_difference,
            _mutate_c59_source_difference,
            _mutate_c59_prerequisite_difference,
            _mutate_c60_closure_evidence_difference,
        ],
        ids=[
            "missing_post_closure_row",
            "malformed_post_closure_id",
            "non_canonical_post_closure_id",
            "extra_post_closure_row",
            "duplicate_post_closure_row",
            "missing_origin_id",
            "extra_origin_id",
            "duplicate_origin_id",
            "csv_json_row_mismatch",
            "register_count_mismatch",
            "field_mismatch",
            "catalog_mismatch",
            "c59_owner_difference",
            "c59_source_difference",
            "c59_prerequisite_difference",
            "c60_closure_evidence_difference",
        ],
    )
    def test_live_route_invariants_reject_structural_break(self, mutate):
        repository = Path(__file__).resolve().parents[3]
        csv_items, routing = _load_live_route_data(repository)
        csv_items = copy.deepcopy(csv_items)
        routing = copy.deepcopy(routing)
        mutate(csv_items, routing)
        with pytest.raises(AssertionError):
            _assert_live_route_invariants(csv_items, routing)


# ---------------------------------------------------------------------------
# User-approved V0 deferrals in entry-gate derivation
# ---------------------------------------------------------------------------

class TestDeferredV0EntryGate:
    def _prepare_deferred_v0_gate(self, write_task, make_repo, make_plan) -> None:
        _write_v0_deferrals(make_plan, DEFERRED_ROWS)
        for task_id in DEFERRED_TASK_IDS:
            write_task(task_id=task_id, status="Blocked")
        _git(make_repo, "checkout", "--", "plan")

    def test_deferred_v0_tasks_close_open_v0_entry_gate(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_deferred_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 0
        assert result["metadata_errors"] == []
        assert result["valid"] is True

    def test_non_deferred_v0_task_keeps_gate_open(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_deferred_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V0-DOM-999", status="Planned")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 1
        assert any("GATE-V0-EXIT is open" in error for error in result["metadata_errors"])
        assert any("V0-DOM-999 (Planned)" in error for error in result["metadata_errors"])

    def test_missing_deferral_table_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        write_task(task_id="V0-DOM-001", status="Planned")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 1
        assert any("GATE-V0-EXIT is open" in error for error in result["metadata_errors"])
        assert any("V0-DOM-001 (Planned)" in error for error in result["metadata_errors"])

    def test_duplicate_deferral_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        _write_v0_deferrals(make_plan, DEFERRED_ROWS + [DEFERRED_ROWS[0]])
        for task_id in DEFERRED_TASK_IDS:
            write_task(task_id=task_id, status="Blocked")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 1
        assert any("duplicate Task ID" in error for error in result["metadata_errors"])

    def test_nonmatching_deferral_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        nonmatching_rows = DEFERRED_ROWS[:-1] + [
            "| `V0-BKP-002` | `2026-08-03` | `V15` | Değiştirilmiş kanıt metni | Not V0 gate closure evidence |"
        ]
        _write_v0_deferrals(make_plan, nonmatching_rows)
        for task_id in DEFERRED_TASK_IDS:
            write_task(task_id=task_id, status="Blocked")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    def test_malformed_deferral_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        malformed_rows = DEFERRED_ROWS.copy()
        malformed_rows[0] = "| `V0-HUG-001` | malformed |"
        _write_v0_deferrals(make_plan, malformed_rows)
        for task_id in DEFERRED_TASK_IDS:
            write_task(task_id=task_id, status="Blocked")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V1-FND-010")

        exit_code, result = run_tool("V1-FND-010", make_repo, make_plan)

        assert exit_code == 1
        assert any("invalid record" in error for error in result["metadata_errors"])

    def test_deferral_does_not_apply_to_other_gates(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_deferred_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-003", status="Planned")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V11-ALT-001")

        exit_code, result = run_tool("V11-ALT-001", make_repo, make_plan)

        assert exit_code == 1
        assert any("GATE-V1-EXIT is open" in error for error in result["metadata_errors"])


# ---------------------------------------------------------------------------
# Evidence directory
# ---------------------------------------------------------------------------

class TestEvidenceDirectory:
    def test_evidence_directory_allowed(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "evidence/V1-FND-003/result.json", "{}\n")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_other_task_evidence_rejected(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _write(make_repo, "evidence/V1-FND-004/result.json", "{}\n")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert any("v1-fnd-004" in p for p in paths)


# ---------------------------------------------------------------------------
# Metadata file
# ---------------------------------------------------------------------------

class TestMetadataFile:
    def test_metadata_file_allowed(self, write_task, make_repo, make_plan, run_tool):
        task_file = write_task()
        _git(make_repo, "add", str(task_file.relative_to(make_repo)))
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Glob matching
# ---------------------------------------------------------------------------

class TestGlobMatching:
    def test_double_star_matches_nested(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`")
        _write(make_repo, "tools/task-scope/deeply/nested/path/file.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_exact_path_match(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `.github/workflows/task-scope.yml`")
        _write(make_repo, ".github/workflows/task-scope.yml")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_exact_path_does_not_match_subpath(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `.github/workflows/task-scope.yml`")
        _write(make_repo, ".github/workflows/other.yml")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert ".github/workflows/other.yml" in paths

    def test_single_star_does_not_cross_directories(
        self, write_task, make_repo, make_plan, run_tool
    ):
        write_task(owned_surface="- `tools/task-scope/*.py`")
        _write(make_repo, "tools/task-scope/file.py")
        _write(make_repo, "tools/task-scope/sub/file.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "tools/task-scope/sub/file.py" in paths
        assert "tools/task-scope/file.py" not in paths


# ---------------------------------------------------------------------------
# Dirty worktree
# ---------------------------------------------------------------------------

class TestDirtyWorktree:
    def test_dirty_worktree_out_of_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task()
        _commit(make_repo, "src/existing.py", "old\n")
        _write(make_repo, "src/existing.py", "modified\n")
        _write(make_repo, "tools/task-scope/new.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "src/existing.py" in paths

    def test_dirty_worktree_all_in_scope(self, write_task, make_repo, make_plan, run_tool):
        write_task(owned_surface="- `tools/task-scope/**`\n- `tests/architecture/taskscope/**`")
        _commit(make_repo, "tools/task-scope/existing.py", "old\n")
        _write(make_repo, "tools/task-scope/existing.py", "modified\n")
        _write(make_repo, "tests/architecture/taskscope/new_test.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Deterministic output
# ---------------------------------------------------------------------------

class TestDeterministicOutput:
    def test_same_result_on_repeated_runs(
        self, write_task, make_repo, make_plan, run_tool
    ):
        write_task()
        _write(make_repo, "src/a.py")
        _write(make_repo, "src/b.py")
        _write(make_repo, "src/c.py")
        _, result1 = run_tool("V1-FND-003", make_repo, make_plan)
        _, result2 = run_tool("V1-FND-003", make_repo, make_plan)
        assert result1 == result2

    def test_findings_contain_all_violations(
        self, write_task, make_repo, make_plan, run_tool
    ):
        write_task()
        _write(make_repo, "src/a.py")
        _write(make_repo, "docs/b.md")
        _write(make_repo, "plan/v1/foundation/other.md")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = {f["path"] for f in result["findings"]}
        assert "src/a.py" in paths
        assert "docs/b.md" in paths
        assert "plan/v1/foundation/other.md" in paths


# ---------------------------------------------------------------------------
# Broken Markdown
# ---------------------------------------------------------------------------

class TestBrokenMarkdown:
    def test_broken_markdown_no_task_id(self, make_repo, make_plan, run_tool):
        task_file = make_plan / "V1-FND-003.md"
        task_file.write_text("# Broken\n\nNo task ID here.\n", encoding="utf-8")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert len(result["metadata_errors"]) > 0

    def test_broken_markdown_no_status(self, make_repo, make_plan, run_tool):
        task_file = make_plan / "V1-FND-003.md"
        task_file.write_text(
            "# V1-FND-003\n\n- Task ID: V1-FND-003\n- Assignee: test\n",
            encoding="utf-8",
        )
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Status" in e for e in result["metadata_errors"])

    def test_broken_markdown_no_assignee(self, make_repo, make_plan, run_tool):
        task_file = make_plan / "V1-FND-003.md"
        task_file.write_text(
            "# V1-FND-003\n\n- Task ID: V1-FND-003\n- Status: InProgress\n",
            encoding="utf-8",
        )
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        assert any("Assignee" in e for e in result["metadata_errors"])

    def test_invalid_task_id_format(self, make_repo, make_plan, run_tool):
        task_file = make_plan / "broken.md"
        task_file.write_text(
            "# Broken\n\n- Task ID: INVALID\n- Status: InProgress\n- Assignee: test\n",
            encoding="utf-8",
        )
        exit_code, result = run_tool("INVALID", make_repo, make_plan)
        assert exit_code == 1
        assert len(result["metadata_errors"]) > 0


# ---------------------------------------------------------------------------
# Comma-separated owned surface
# ---------------------------------------------------------------------------

class TestCommaSeparatedSurface:
    def test_multiple_paths_on_one_line(
        self, write_task, make_repo, make_plan, run_tool
    ):
        write_task(
            owned_surface="- `tools/task-scope/**`, `tests/architecture/taskscope/**`"
        )
        _write(make_repo, "tools/task-scope/a.py")
        _write(make_repo, "tests/architecture/taskscope/b.py")
        exit_code, result = run_tool("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0


# ---------------------------------------------------------------------------
# Diff mode: task Markdown added in the PR itself
# ---------------------------------------------------------------------------

class TestAddedTaskMarkdownDiffMode:
    def test_added_task_markdown_in_diff_mode_uses_head_baseline(
        self, make_repo, make_plan
    ):
        """A task Markdown file introduced in the PR itself (absent from the
        merge-base) must validate against its own HEAD baseline instead of
        failing with a missing-baseline finding. The surface check still
        applies: the changed paths diffed against the base are allowed."""
        import importlib.util
        from conftest import DONE_TASK_TEMPLATE, VALID_TASK_TEMPLATE

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)

        _write(
            make_repo,
            "plan/V0-DOM-001.md",
            DONE_TASK_TEMPLATE.format(task_id="V0-DOM-001"),
        )
        _git(make_repo, "add", "plan/V0-DOM-001.md")
        _git(make_repo, "commit", "-q", "-m", "add v0 prerequisite")
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _write(
            make_repo,
            "plan/V1-FND-003.md",
            VALID_TASK_TEMPLATE.format(
                task_id="V1-FND-003",
                status="InProgress",
                assignee="real-session-123",
                owned_surface="- `tools/task-scope/**`",
                dependencies="- None",
            ),
        )
        _git(make_repo, "add", "plan/V1-FND-003.md")
        _git(make_repo, "commit", "-q", "-m", "add task markdown")
        result = mod.run_validation("V1-FND-003", make_repo, make_plan, diff_base=base)
        assert result["valid"] is True
        assert result["findings"] == []


# ---------------------------------------------------------------------------
# Diff mode: InProgress -> Done transition
# ---------------------------------------------------------------------------

class TestDoneTransitionDiffMode:
    def _load_module(self):
        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod

    def _baseline(self, make_repo, make_plan, status):
        from conftest import DONE_TASK_TEMPLATE, VALID_TASK_TEMPLATE

        _write(
            make_repo,
            "plan/V0-DOM-001.md",
            DONE_TASK_TEMPLATE.format(task_id="V0-DOM-001"),
        )
        _write(
            make_repo,
            "plan/V1-FND-003.md",
            VALID_TASK_TEMPLATE.format(
                task_id="V1-FND-003",
                status=status,
                assignee="test-session-active",
                owned_surface="- `tools/task-scope/**`",
                dependencies="- None",
            ),
        )
        _git(make_repo, "add", "plan/V0-DOM-001.md", "plan/V1-FND-003.md")
        _git(make_repo, "commit", "-q", "-m", "add task baseline")
        return _git(make_repo, "rev-parse", "HEAD").strip()

    def test_done_transition_from_in_progress_accepted(
        self, make_repo, make_plan
    ):
        """InProgress -> Done in diff mode is a legal closing PR."""
        base = self._baseline(make_repo, make_plan, "InProgress")
        task_file = make_plan / "V1-FND-003.md"
        text = task_file.read_text(encoding="utf-8")
        task_file.write_text(
            text.replace("- Status: InProgress", "- Status: Done"),
            encoding="utf-8",
        )
        _git(make_repo, "add", "plan/V1-FND-003.md")
        _git(make_repo, "commit", "-q", "-m", "close task")
        mod = self._load_module()
        result = mod.run_validation("V1-FND-003", make_repo, make_plan, diff_base=base)
        assert result["metadata_errors"] == []
        assert result["valid"] is True

    def test_done_transition_from_planned_rejected(
        self, make_repo, make_plan
    ):
        """Planned -> Done jumps the executable states and must be rejected."""
        base = self._baseline(make_repo, make_plan, "Planned")
        task_file = make_plan / "V1-FND-003.md"
        text = task_file.read_text(encoding="utf-8")
        task_file.write_text(
            text.replace("- Status: Planned", "- Status: Done"),
            encoding="utf-8",
        )
        _git(make_repo, "add", "plan/V1-FND-003.md")
        _git(make_repo, "commit", "-q", "-m", "close task")
        mod = self._load_module()
        result = mod.run_validation("V1-FND-003", make_repo, make_plan, diff_base=base)
        assert result["valid"] is False
        assert any(
            "expected 'Planned' or 'InProgress'" in e
            for e in result["metadata_errors"]
        )

    def test_closure_evidence_pr_keeps_done_accepted(
        self, make_repo, make_plan
    ):
        """An evidence-only PR after closure (task file already Done on the
        base) must not re-fail on status: Done stays Done."""
        base = self._baseline(make_repo, make_plan, "InProgress")
        task_file = make_plan / "V1-FND-003.md"
        text = task_file.read_text(encoding="utf-8")
        task_file.write_text(
            text.replace("- Status: InProgress", "- Status: Done"),
            encoding="utf-8",
        )
        _git(make_repo, "add", "plan/V1-FND-003.md")
        _git(make_repo, "commit", "-q", "-m", "close task")
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _write(make_repo, "evidence/v1-fnd-003/verification.md", "proof\n")
        _git(make_repo, "add", "evidence/v1-fnd-003/verification.md")
        _git(make_repo, "commit", "-q", "-m", "closure evidence")
        mod = self._load_module()
        result = mod.run_validation("V1-FND-003", make_repo, make_plan, diff_base=base)
        assert result["metadata_errors"] == []
        assert result["valid"] is True
