from __future__ import annotations

"""Validate tamper-evident, replayable task-closure evidence envelopes."""

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any, Callable


SCHEMA = "alkaros.closure-evidence-envelope/v2"
LEGACY_SCHEMA = "alkaros.closure-evidence-envelope/v1"
_TASK_ID = re.compile(r"^V\d+-[A-Z]+-\d+$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_GIT_COMMIT = re.compile(r"^[0-9a-f]{40,64}$")
_SAFE_RELATIVE_PATH = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._/-]*$")
_SECRET_NAME = re.compile(r"(?:password|secret|token|api[_ -]?key)", re.IGNORECASE)
_SECRET_ASSIGNMENT = re.compile(
    r"\b(?:password|secret|token|api[_ -]?key)\b\s*(?:=|:)\s*[^\s]+",
    re.IGNORECASE,
)
_SECRET_FLAG = re.compile(
    r"--(?:password|secret|token|api[_-]?key)\b(?:\s+|=)\S+",
    re.IGNORECASE,
)
_AUTHORIZATION_BEARER = re.compile(r"\bauthorization\s*:\s*bearer\s+\S+", re.IGNORECASE)
_V0_GOV_035_BASELINE = "1d41e97b39ac975ab55c2bdf4198b0d6b92681ed"
_V0_GOV_035_CLOSURE = "78b317a5c3d04009d94394da58c5913d59c22b91"
_V3_TASK_ID = "V1-FND-023"
_V3_B0_COMMIT = "fd3344f15c5257b53bf5281ee9129f800c62f0a7"
_V3_B0_PARENT = "645a07f6992ece6efd70dc2fb2e0a7f7bccc945c"
_V3_INTERRUPTION_COMMIT = "479881636c8142c7161f2d5980d37ca2f9b48591"
_V3_TASK_PATH = "plan/v1/foundation/V1-FND-023-solution-test-discovery.md"
_V3_SOURCE_ARTIFACTS = {
    "Directory.Build.targets": "a4a6e142edcd8d64ec841396aa45c26c720e2922e47169d2c10f076e0d2e71dd",
    "tests/Architecture/TestDiscovery/test_solution_test_discovery.py": "05b27d3dddcb7882a586ea01fbb81495818e84aa0119485fe3d68f2d70fca764",
}
_V3_INTERRUPTION_BLOCKER = (
    "## Blocker\n\n"
    "- Command: `py -B tools\\\\plan-audit\\\\plan_audit_tool.py validate`\n"
    "- Result: exit code `1` with `APPLICATION_STARTED_BEFORE_V0_EXIT V1-FND-023`.\n"
    "- Unlock: `V0-GOV-054` plan denetiminin C52/C53/C54 kabulünü tanımasını "
    "düzeltmelidir; ancak bu tamamlandığında görev sürdürülebilir.\n"
)


def canonical_payload_hash(envelope: dict[str, Any]) -> str:
    """Return the SHA-256 of the envelope excluding its integrity field."""
    payload = {key: value for key, value in envelope.items() if key != "integrity"}
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(encoded.encode("utf-8")).hexdigest()


def _error(errors: list[dict[str, str]], code: str, message: str) -> None:
    errors.append({"code": code, "message": message})


def _safe_path(value: object, errors: list[dict[str, str]], field: str) -> str | None:
    if not isinstance(value, str) or not _SAFE_RELATIVE_PATH.fullmatch(value):
        _error(errors, "INVALID_PATH", f"{field} must be a safe repository-relative path")
        return None
    path = Path(value)
    if path.is_absolute() or ".." in path.parts:
        _error(errors, "INVALID_PATH", f"{field} must not escape the repository")
        return None
    return path.as_posix()


def _sha256(value: object, errors: list[dict[str, str]], field: str) -> str | None:
    if not isinstance(value, str) or not _SHA256.fullmatch(value):
        _error(errors, "INVALID_SHA256", f"{field} must be a lowercase SHA-256 value")
        return None
    return value


def _contains_secret(value: str) -> bool:
    return bool(_SECRET_ASSIGNMENT.search(value) or _SECRET_FLAG.search(value) or _AUTHORIZATION_BEARER.search(value))


def _git(repo: Path, *args: str, input: bytes | None = None) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        check=False,
        input=input,
    )


def _git_commit_exists(repo: Path, commit: str) -> bool:
    return _git(repo, "rev-parse", "--verify", f"{commit}^{{commit}}").returncode == 0


def _git_blob(repo: Path, commit: str, path: str) -> bytes | None:
    result = _git(repo, "show", f"{commit}:{path}")
    return result.stdout if result.returncode == 0 else None


def _git_text(repo: Path, commit: str, path: str) -> str | None:
    blob = _git_blob(repo, commit, path)
    if blob is None:
        return None
    try:
        return blob.decode("utf-8")
    except UnicodeDecodeError:
        return None


def _is_stale_candidate(repo: Path, candidate_commit: str, path: str, reference_commit: str = "HEAD") -> bool:
    if _git(repo, "rev-parse", "--verify", f"{reference_commit}^{{commit}}").returncode != 0:
        return True
    if _git(repo, "merge-base", "--is-ancestor", candidate_commit, reference_commit).returncode != 0:
        return True
    return _git(repo, "diff", "--quiet", f"{candidate_commit}..{reference_commit}", "--", path).returncode != 0


def _validate_environment(value: object, errors: list[dict[str, str]]) -> None:
    if not isinstance(value, dict):
        _error(errors, "MISSING_ENVIRONMENT", "environment must be an object")
        return
    if set(value) != {"platform", "toolchain", "variables", "secrets"}:
        _error(errors, "INVALID_ENVIRONMENT", "environment must contain only platform, toolchain, variables, and secrets")
        return
    if not isinstance(value["platform"], str) or not value["platform"].strip():
        _error(errors, "INVALID_ENVIRONMENT", "environment.platform is required")
    for field in ("toolchain", "variables"):
        items = value[field]
        if not isinstance(items, dict) or (field == "toolchain" and not items):
            _error(errors, "INVALID_ENVIRONMENT", f"environment.{field} must be a {'non-empty ' if field == 'toolchain' else ''}object")
            continue
        for name, item in items.items():
            if not isinstance(name, str) or not isinstance(item, str) or _SECRET_NAME.search(name) or _contains_secret(item):
                _error(errors, "SECRET_LEAKAGE", f"environment.{field} may not contain a sensitive name or value")
    if not isinstance(value["secrets"], list):
        _error(errors, "INVALID_ENVIRONMENT", "environment.secrets must be a list")
        return
    for index, secret in enumerate(value["secrets"]):
        if not isinstance(secret, dict) or set(secret) != {"location", "fingerprint"}:
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}] must contain only location and fingerprint")
            continue
        location, fingerprint = secret["location"], secret["fingerprint"]
        if not isinstance(location, str) or not location.startswith("env:"):
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}].location must be redacted")
        if not isinstance(fingerprint, str) or not fingerprint.startswith("sha256:") or not _SHA256.fullmatch(fingerprint.removeprefix("sha256:")):
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}].fingerprint is invalid")


def _worktree_blob(repository: Path, path: str) -> bytes | None:
    artifact = repository / path
    return artifact.read_bytes() if artifact.is_file() else None


def _validate_raw_output(
    value: object,
    read_blob: Callable[[str], bytes | None],
    task_id: str,
    errors: list[dict[str, str]],
    field: str,
) -> None:
    if not isinstance(value, dict) or set(value) != {"path", "sha256"}:
        _error(errors, "INVALID_RAW_OUTPUT", f"{field} must contain path and sha256")
        return
    path = _safe_path(value["path"], errors, f"{field}.path")
    expected_hash = _sha256(value["sha256"], errors, f"{field}.sha256")
    if path is None or expected_hash is None:
        return
    if not path.startswith(f"evidence/{task_id}/"):
        _error(errors, "INVALID_RAW_OUTPUT", f"{field}.path must stay under evidence/{task_id}/")
        return
    content = read_blob(path)
    if content is None:
        _error(errors, "MISSING_RAW_OUTPUT", f"{field}.path is missing: {path}")
        return
    if hashlib.sha256(content).hexdigest() != expected_hash:
        _error(errors, "RAW_OUTPUT_HASH_MISMATCH", f"{field}.path hash does not match")
    if _contains_secret(content.decode("utf-8", errors="replace")):
        _error(errors, "SECRET_LEAKAGE", f"{field}.path contains a secret value")


def _validate_commands(
    value: object,
    read_blob: Callable[[str], bytes | None],
    task_id: str,
    errors: list[dict[str, str]],
) -> None:
    if not isinstance(value, list) or not value:
        _error(errors, "MISSING_COMMAND", "commands must contain at least one command record")
        return
    for index, command in enumerate(value):
        field = f"commands[{index}]"
        if not isinstance(command, dict) or set(command) != {"command", "exit_code", "raw_output"}:
            _error(errors, "MISSING_EXIT_CODE" if isinstance(command, dict) and "exit_code" not in command else "INVALID_COMMAND", f"{field} must contain command, exit_code, and raw_output")
            continue
        command_text = command["command"]
        if not isinstance(command_text, str) or not command_text.strip():
            _error(errors, "MISSING_COMMAND", f"{field}.command is required")
        elif _contains_secret(command_text):
            _error(errors, "SECRET_LEAKAGE", f"{field}.command contains a secret value")
        exit_code = command["exit_code"]
        if type(exit_code) is not int:
            _error(errors, "MISSING_EXIT_CODE", f"{field}.exit_code must be an integer")
        elif exit_code != 0:
            _error(errors, "NONZERO_EXIT_CODE", f"{field}.exit_code must be 0 for closure evidence")
        _validate_raw_output(command["raw_output"], read_blob, task_id, errors, f"{field}.raw_output")


def _validate_source_artifacts(
    value: object,
    repo: Path,
    subject_commit: str | None,
    errors: list[dict[str, str]],
    reference_commit: str = "HEAD",
) -> set[str]:
    paths: set[str] = set()
    if not isinstance(value, list) or not value:
        _error(errors, "MISSING_ARTIFACT_HASH", "artifacts must contain at least one source blob")
        return paths
    for index, artifact in enumerate(value):
        field = f"artifacts[{index}]"
        if not isinstance(artifact, dict) or set(artifact) != {"path", "sha256"}:
            _error(errors, "INVALID_ARTIFACT", f"{field} must contain path and sha256")
            continue
        path = _safe_path(artifact["path"], errors, f"{field}.path")
        expected_hash = _sha256(artifact["sha256"], errors, f"{field}.sha256")
        if path is None or expected_hash is None:
            continue
        if path in paths:
            _error(errors, "DUPLICATE_ARTIFACT", f"{field}.path is duplicated: {path}")
            continue
        paths.add(path)
        if subject_commit is None:
            continue
        blob = _git_blob(repo, subject_commit, path)
        if blob is None or hashlib.sha256(blob).hexdigest() != expected_hash:
            _error(errors, "FINAL_BLOB_HASH_MISMATCH", f"subject blob hash differs: {path}")
        if _is_stale_candidate(repo, subject_commit, path, reference_commit):
            _error(errors, "STALE_CANDIDATE_COMMIT", f"subject commit is stale for artifact: {path}")
    return paths


def _load_envelope_bytes(value: bytes) -> dict[str, Any] | None:
    try:
        loaded = json.loads(value.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    return loaded if isinstance(loaded, dict) else None


def _load_envelope(envelope_path: Path) -> dict[str, Any] | None:
    try:
        return _load_envelope_bytes(envelope_path.read_bytes())
    except FileNotFoundError:
        return None


def _validate_envelope(
    envelope: dict[str, Any],
    repository: Path,
    read_raw_blob: Callable[[str], bytes | None],
    reference_commit: str = "HEAD",
) -> dict[str, object]:
    """Validate one loaded envelope using the supplied raw-evidence blob reader."""
    errors: list[dict[str, str]] = []
    schema = envelope.get("schema")
    subject_field = "subject_commit" if schema == SCHEMA else "candidate_commit"
    required = {"schema", "task_id", subject_field, "environment", "commands", "artifacts", "integrity"}
    if set(envelope) != required:
        _error(errors, "NARRATIVE_ONLY_EVIDENCE", "envelope must contain exactly the closure schema fields")
        return {"valid": False, "errors": errors}
    if schema not in {SCHEMA, LEGACY_SCHEMA}:
        _error(errors, "INVALID_SCHEMA", f"schema must equal {SCHEMA}")
    task_id = envelope["task_id"]
    if not isinstance(task_id, str) or not _TASK_ID.fullmatch(task_id):
        _error(errors, "INVALID_TASK_ID", "task_id must use the canonical task ID format")
        task_id = "INVALID"
    subject_commit = envelope[subject_field]
    if not isinstance(subject_commit, str) or not _GIT_COMMIT.fullmatch(subject_commit):
        _error(errors, "MISSING_CANDIDATE_COMMIT", f"{subject_field} must be a full Git commit hash")
        subject_commit = None
    elif not _git_commit_exists(repository, subject_commit):
        _error(errors, "STALE_CANDIDATE_COMMIT", f"{subject_field} does not resolve to a commit")
        subject_commit = None
    _validate_environment(envelope["environment"], errors)
    _validate_commands(envelope["commands"], read_raw_blob, task_id, errors)
    _validate_source_artifacts(envelope["artifacts"], repository, subject_commit, errors, reference_commit)
    integrity = envelope["integrity"]
    if not isinstance(integrity, dict) or set(integrity) != {"payload_sha256"}:
        _error(errors, "INVALID_INTEGRITY", "integrity must contain only payload_sha256")
    elif integrity["payload_sha256"] != canonical_payload_hash(envelope):
        _error(errors, "INTEGRITY_HASH_MISMATCH", "integrity.payload_sha256 does not match the envelope")
    return {"valid": not errors, "errors": errors}


def validate_envelope(envelope_path: Path, repository: Path) -> dict[str, object]:
    """Return a deterministic validation result for one closure-evidence envelope."""
    envelope = _load_envelope(envelope_path)
    if envelope is None:
        return {"valid": False, "errors": [{"code": "INVALID_ENVELOPE", "message": str(envelope_path)}]}
    return _validate_envelope(envelope, repository, lambda path: _worktree_blob(repository, path))


def _commit_parent(repo: Path, commit: str) -> str | None:
    result = _git(repo, "rev-parse", f"{commit}^")
    return result.stdout.decode().strip() if result.returncode == 0 else None


def _commit_parents(repo: Path, commit: str) -> tuple[str, ...] | None:
    result = _git(repo, "show", "-s", "--format=%P", commit)
    if result.returncode != 0:
        return None
    return tuple(result.stdout.decode().split())


def _changed_paths(repo: Path, older: str, newer: str) -> set[str]:
    result = _git(repo, "diff", "--name-only", older, newer)
    return set(result.stdout.decode().splitlines()) if result.returncode == 0 else set()


def _changed_path_statuses(repo: Path, older: str, newer: str) -> set[tuple[str, str]] | None:
    result = _git(repo, "diff", "--name-status", "--no-renames", older, newer)
    if result.returncode != 0:
        return None
    parsed: set[tuple[str, str]] = set()
    for line in result.stdout.decode().splitlines():
        status, separator, path = line.partition("\t")
        if not separator or status not in {"A", "M", "D"}:
            return None
        parsed.add((status, path))
    return parsed


def _task_path(repo: Path, commit: str, task_id: str) -> str | None:
    result = _git(repo, "grep", "-l", "-e", f"- Task ID: {task_id}", commit, "--", "plan")
    paths = result.stdout.decode().splitlines() if result.returncode == 0 else []
    return paths[0].removeprefix(f"{commit}:") if len(paths) == 1 else None


def _metadata(task_text: str) -> tuple[str, str] | None:
    status = re.search(r"^- Status: (.+)$", task_text, re.MULTILINE)
    assignee = re.search(r"^- Assignee: (.+)$", task_text, re.MULTILINE)
    return (status.group(1), assignee.group(1)) if status and assignee else None


def _owned_surface_paths(task_text: str) -> set[str] | None:
    match = re.search(r"^## Owned surface\r?\n(.*?)(?=^## )", task_text, re.MULTILINE | re.DOTALL)
    if match is None:
        return None
    paths = set(re.findall(r"^- `([^`]+)`$", match.group(1), re.MULTILINE))
    if not paths or any(
        not path.startswith("evidence/") and ("*" in path or "?" in path) for path in paths
    ):
        return None
    return paths


def _parse_trailers(repo: Path, commit: str) -> list[str] | None:
    message = _git(repo, "show", "-s", "--format=%B", commit)
    parsed = _git(repo, "interpret-trailers", "--parse", input=message.stdout)
    return parsed.stdout.decode().splitlines() if parsed.returncode == 0 else None


def _task_without_blocker(task_text: str) -> str | None:
    match = re.search(r"\n## Blocker\r?\n.*?(?=\n## )", task_text, re.DOTALL)
    if match is None or task_text.count("\n## Blocker\n") + task_text.count("\n## Blocker\r\n") != 1:
        return None
    return task_text[:match.start()] + task_text[match.end():]


def _v3_error(errors: list[dict[str, str]], code: str, message: str) -> None:
    _error(errors, code, message)


def validate_v3_interrupted_final_commit(final_commit: str, repository: Path) -> dict[str, object]:
    """Verify the fixed V1-FND-023 interrupted-source closure topology."""
    errors: list[dict[str, str]] = []
    if not _git_commit_exists(repository, final_commit):
        return {"valid": False, "errors": [{"code": "MISSING_FINAL_COMMIT", "message": final_commit}]}

    final_parents = _commit_parents(repository, final_commit)
    if final_parents is None or len(final_parents) != 1:
        _v3_error(errors, "V3_INVALID_TOPOLOGY", "final commit must have exactly one evidence parent")
        return {"valid": False, "errors": errors}
    evidence_commit = final_parents[0]
    evidence_parents = _commit_parents(repository, evidence_commit)
    if evidence_parents is None or len(evidence_parents) != 1:
        _v3_error(errors, "V3_INVALID_TOPOLOGY", "evidence commit must have exactly one reentry parent")
        return {"valid": False, "errors": errors}
    reentry_commit = evidence_parents[0]
    reentry_parents = _commit_parents(repository, reentry_commit)
    if reentry_parents is None or len(reentry_parents) != 1:
        _v3_error(errors, "V3_INVALID_TOPOLOGY", "reentry must have exactly one V0-GOV-055 final parent")
        return {"valid": False, "errors": errors}
    v055_final = reentry_parents[0]

    v055_result = validate_final_commit(v055_final, repository)
    if not v055_result["valid"]:
        _v3_error(errors, "V3_REENTRY_PARENT_NOT_V055_FINAL", "reentry parent must be a valid V0-GOV-055 v2 final")
    else:
        v055_trailers = _parse_trailers(repository, v055_final)
        if v055_trailers is None or not v055_trailers or v055_trailers[0] != "Task: V0-GOV-055":
            _v3_error(errors, "V3_REENTRY_PARENT_NOT_V055_FINAL", "reentry parent must close V0-GOV-055")

    if _git(repository, "merge-base", "--is-ancestor", _V3_INTERRUPTION_COMMIT, v055_final).returncode != 0:
        _v3_error(errors, "V3_INVALID_TOPOLOGY", "V0-GOV-055 final must descend from the fixed interruption")

    b0_parents = _commit_parents(repository, _V3_B0_COMMIT)
    interruption_parents = _commit_parents(repository, _V3_INTERRUPTION_COMMIT)
    if b0_parents != (_V3_B0_PARENT,):
        _v3_error(errors, "V3_INVALID_B0_PARENT", "B0 must retain its fixed single parent")
    if interruption_parents != (_V3_B0_COMMIT,):
        _v3_error(errors, "V3_INVALID_INTERRUPTION", "interruption must be the fixed direct B0 child")

    expected_b0_changes = {
        ("M", "Directory.Build.targets"),
        ("M", _V3_TASK_PATH),
        ("A", "tests/Architecture/TestDiscovery/test_solution_test_discovery.py"),
    }
    if _changed_path_statuses(repository, _V3_B0_PARENT, _V3_B0_COMMIT) != expected_b0_changes:
        _v3_error(errors, "V3_INVALID_B0_DIFF", "B0 must retain its exact three-path source and task diff")

    b0_parent_task = _git_text(repository, _V3_B0_PARENT, _V3_TASK_PATH)
    b0_task = _git_text(repository, _V3_B0_COMMIT, _V3_TASK_PATH)
    if b0_parent_task is None or b0_task is None:
        _v3_error(errors, "V3_INVALID_B0_METADATA", "B0 task metadata must be readable")
    else:
        expected_b0_task = b0_parent_task.replace("- Status: Planned", "- Status: InProgress", 1).replace(
            "- Assignee: Unassigned (exactly one person)",
            "- Assignee: /root/implement_v1_fnd_023",
            1,
        )
        if b0_task != expected_b0_task:
            _v3_error(errors, "V3_INVALID_B0_METADATA", "B0 must make only the exact Planned to InProgress metadata transition")

    for path, expected_hash in _V3_SOURCE_ARTIFACTS.items():
        blob = _git_blob(repository, _V3_B0_COMMIT, path)
        if blob is None or hashlib.sha256(blob).hexdigest() != expected_hash:
            _v3_error(errors, "V3_B0_BLOB_MISMATCH", f"B0 source blob differs: {path}")

    interruption_task = _git_text(repository, _V3_INTERRUPTION_COMMIT, _V3_TASK_PATH)
    if _changed_path_statuses(repository, _V3_B0_COMMIT, _V3_INTERRUPTION_COMMIT) != {("M", _V3_TASK_PATH)}:
        _v3_error(errors, "V3_INVALID_INTERRUPTION_DIFF", "interruption may change only the V1-FND-023 task")
    elif b0_task is None or interruption_task is None:
        _v3_error(errors, "V3_INVALID_INTERRUPTION", "interruption task metadata must be readable")
    else:
        expected_interruption = b0_task.replace("- Status: InProgress", "- Status: Blocked", 1).replace(
            "\n## Deliverables\n",
            f"\n{_V3_INTERRUPTION_BLOCKER}\n## Deliverables\n",
            1,
        )
        if interruption_task != expected_interruption:
            _v3_error(errors, "V3_INVALID_INTERRUPTION_DIFF", "interruption must add only the exact blocker")

    parent_task = _git_text(repository, v055_final, _V3_TASK_PATH)
    reentry_task = _git_text(repository, reentry_commit, _V3_TASK_PATH)
    if _changed_path_statuses(repository, v055_final, reentry_commit) != {("M", _V3_TASK_PATH)}:
        _v3_error(errors, "V3_INVALID_REENTRY_DIFF", "reentry may change only the V1-FND-023 task")
    elif parent_task is None or reentry_task is None or _metadata(parent_task) is None or _metadata(reentry_task) is None:
        _v3_error(errors, "V3_INVALID_REENTRY_METADATA", "reentry task metadata must be readable")
    else:
        expected_reentry = _task_without_blocker(parent_task)
        if (
            _metadata(parent_task)[0] != "Blocked"
            or _metadata(reentry_task)[0] != "InProgress"
            or _metadata(parent_task)[1] != _metadata(reentry_task)[1]
            or expected_reentry is None
            or reentry_task != expected_reentry.replace("- Status: Blocked", "- Status: InProgress", 1)
        ):
            _v3_error(errors, "V3_INVALID_REENTRY_DIFF", "reentry must remove exactly one blocker and change only Blocked to InProgress")

    for commit in (v055_final, reentry_commit, evidence_commit, final_commit):
        for path, expected_hash in _V3_SOURCE_ARTIFACTS.items():
            blob = _git_blob(repository, commit, path)
            if blob is None or hashlib.sha256(blob).hexdigest() != expected_hash:
                _v3_error(errors, "V3_SOURCE_BLOB_MISMATCH", f"source blob differs at {commit}: {path}")

    evidence_changes = _changed_path_statuses(repository, reentry_commit, evidence_commit)
    envelope_path = f"evidence/{_V3_TASK_ID}/closure-evidence-envelope.json"
    if not evidence_changes or any(status != "A" or not path.startswith(f"evidence/{_V3_TASK_ID}/") for status, path in evidence_changes):
        _v3_error(errors, "V3_EVIDENCE_NOT_CHECKPOINT_ONLY", "evidence checkpoint may add only V1-FND-023 evidence")
    envelope_blob = _git_blob(repository, evidence_commit, envelope_path)
    envelope = _load_envelope_bytes(envelope_blob) if envelope_blob is not None else None
    if envelope is None or envelope.get("schema") != SCHEMA or envelope.get("task_id") != _V3_TASK_ID or envelope.get("subject_commit") != _V3_B0_COMMIT:
        _v3_error(errors, "V3_INVALID_CLOSURE_ENVELOPE", "v3 envelope must bind V1-FND-023 and the fixed B0 subject")
        return {"valid": False, "errors": errors}
    envelope_result = _validate_envelope(
        envelope,
        repository,
        lambda path: _git_blob(repository, evidence_commit, path),
        final_commit,
    )
    if not envelope_result["valid"]:
        errors.extend(envelope_result["errors"])
    artifact_paths = _validate_source_artifacts(
        envelope.get("artifacts"), repository, _V3_B0_COMMIT, errors, final_commit
    )
    if artifact_paths != set(_V3_SOURCE_ARTIFACTS):
        _v3_error(errors, "V3_SOURCE_ARTIFACT_SET_MISMATCH", "v3 envelope must hash exactly the two B0 source artifacts")
    raw_paths = {
        record.get("raw_output", {}).get("path")
        for record in envelope.get("commands", [])
        if isinstance(record, dict)
    }
    if not raw_paths or not all(isinstance(path, str) and ("A", path) in evidence_changes for path in raw_paths):
        _v3_error(errors, "V3_EVIDENCE_RAW_NOT_CHECKPOINTED", "every raw command output must be added by the evidence checkpoint")
    checkpoint_paths = {envelope_path} | {path for path in raw_paths if isinstance(path, str)}
    if not all(("A", path) in evidence_changes for path in checkpoint_paths):
        _v3_error(errors, "V3_EVIDENCE_NOT_CHECKPOINT_ONLY", "envelope and raw outputs must be checkpoint additions")
    if any(_worktree_blob(repository, path) != _git_blob(repository, evidence_commit, path) for path in checkpoint_paths):
        _v3_error(errors, "WORKTREE_EVIDENCE_SUBSTITUTION", "worktree evidence differs from the v3 evidence checkpoint tree")

    trailers = _parse_trailers(repository, final_commit)
    expected_trailers = [
        f"Task: {_V3_TASK_ID}",
        "Gate: GATE-V0-EXIT",
        f"Closure-Subject: {_V3_B0_COMMIT}",
        f"Closure-Interruption: {_V3_INTERRUPTION_COMMIT}",
        f"Closure-Reentry: {reentry_commit}",
        f"Closure-Evidence-Checkpoint: {evidence_commit}",
    ]
    if trailers != expected_trailers:
        _v3_error(errors, "V3_INVALID_FINAL_TRAILERS", "v3 final must carry exactly the ordered six-trailer block")
    final_task = _git_text(repository, final_commit, _V3_TASK_PATH)
    if _changed_path_statuses(repository, evidence_commit, final_commit) != {("M", _V3_TASK_PATH)}:
        _v3_error(errors, "V3_FINAL_NOT_METADATA_ONLY", "v3 final may change only the V1-FND-023 task")
    elif reentry_task is None or final_task is None or _metadata(reentry_task) is None or _metadata(final_task) is None or (
        re.sub(r"^- Status: .+$", "- Status: <status>", reentry_task, flags=re.MULTILINE)
        != re.sub(r"^- Status: .+$", "- Status: <status>", final_task, flags=re.MULTILINE)
    ) or _metadata(reentry_task)[0] != "InProgress" or _metadata(final_task)[0] != "Done":
        _v3_error(errors, "V3_FINAL_NOT_METADATA_ONLY", "v3 final must change only InProgress to Done")
    return {"valid": not errors, "errors": errors}


def validate_v1_fnd_023_v3_final_commit(final_commit: str, repository: Path) -> dict[str, object]:
    """Validate only the fixed V1-FND-023 interrupted-remediation final."""
    return validate_v3_interrupted_final_commit(final_commit, repository)


def validate_final_commit(final_commit: str, repository: Path) -> dict[str, object]:
    """Verify a v2 B -> E -> F closure chain ending at ``final_commit``."""
    errors: list[dict[str, str]] = []
    if not _git_commit_exists(repository, final_commit):
        return {"valid": False, "errors": [{"code": "MISSING_FINAL_COMMIT", "message": final_commit}]}
    trailers = _parse_trailers(repository, final_commit)
    if trailers and trailers[0] == f"Task: {_V3_TASK_ID}":
        return validate_v3_interrupted_final_commit(final_commit, repository)
    evidence_commit = _commit_parent(repository, final_commit)
    if evidence_commit is None:
        _error(errors, "INVALID_CLOSURE_CHAIN", "final commit must have an evidence parent")
        return {"valid": False, "errors": errors}
    subject_commit = _commit_parent(repository, evidence_commit)
    if subject_commit is None:
        _error(errors, "INVALID_CLOSURE_CHAIN", "evidence commit must have a subject parent")
        return {"valid": False, "errors": errors}
    task_id = None
    if trailers is None or len(trailers) != 4:
        _error(errors, "INVALID_FINAL_TRAILERS", "final commit must contain exactly four Git trailers")
    else:
        values = dict(line.split(": ", 1) for line in trailers if ": " in line)
        task_id = values.get("Task")
        expected = [
            f"Task: {task_id}",
            "Gate: GATE-V0-EXIT",
            f"Closure-Subject: {subject_commit}",
            f"Closure-Evidence-Checkpoint: {evidence_commit}",
        ]
        if not isinstance(task_id, str) or not _TASK_ID.fullmatch(task_id) or trailers != expected:
            _error(errors, "INVALID_FINAL_TRAILERS", "final trailers must be adjacent, parsed, ordered, and self-binding")
    if task_id is None:
        return {"valid": False, "errors": errors}
    task_path = _task_path(repository, final_commit, task_id)
    if task_path is None:
        _error(errors, "INVALID_FINAL_METADATA", "task metadata file must resolve uniquely")
        return {"valid": False, "errors": errors}
    final_changes = _changed_paths(repository, evidence_commit, final_commit)
    if final_changes != {task_path}:
        _error(errors, "FINAL_NOT_METADATA_ONLY", "final commit may change only the active task metadata file")
    evidence_task = _git_text(repository, evidence_commit, task_path)
    final_task = _git_text(repository, final_commit, task_path)
    if evidence_task is None or final_task is None or _metadata(evidence_task) is None or _metadata(final_task) is None:
        _error(errors, "INVALID_FINAL_METADATA", "task metadata must be readable at evidence and final commits")
    elif _metadata(evidence_task)[0] != "InProgress" or _metadata(final_task)[0] != "Done" or _metadata(evidence_task)[1] != _metadata(final_task)[1] or re.sub(r"^- Status: .+$", "- Status: <status>", evidence_task, flags=re.MULTILINE) != re.sub(r"^- Status: .+$", "- Status: <status>", final_task, flags=re.MULTILINE):
        _error(errors, "FINAL_NOT_METADATA_ONLY", "final diff must be exactly Status: InProgress to Status: Done")
    evidence_changes = _changed_paths(repository, subject_commit, evidence_commit)
    envelope_path = f"evidence/{task_id}/closure-evidence-envelope.json"
    if not evidence_changes or envelope_path not in evidence_changes or any(not path.startswith(f"evidence/{task_id}/") for path in evidence_changes):
        _error(errors, "EVIDENCE_NOT_CHECKPOINT_ONLY", "evidence commit may change only the active task evidence checkpoint")
    envelope_blob = _git_blob(repository, evidence_commit, envelope_path)
    envelope = _load_envelope_bytes(envelope_blob) if envelope_blob is not None else None
    if envelope is None or envelope.get("schema") != SCHEMA or envelope.get("task_id") != task_id or envelope.get("subject_commit") != subject_commit:
        _error(errors, "INVALID_CLOSURE_ENVELOPE", "v2 envelope must bind the active task and subject commit")
        return {"valid": False, "errors": errors}
    envelope_result = _validate_envelope(
        envelope,
        repository,
        lambda path: _git_blob(repository, evidence_commit, path),
    )
    if not envelope_result["valid"]:
        errors.extend(envelope_result["errors"])
    raw_paths = {record.get("raw_output", {}).get("path") for record in envelope.get("commands", []) if isinstance(record, dict)}
    if not all(isinstance(path, str) and path in evidence_changes for path in raw_paths):
        _error(errors, "EVIDENCE_RAW_NOT_CHECKPOINTED", "every raw command output must be added by the evidence checkpoint")
    checkpoint_paths = {envelope_path} | {path for path in raw_paths if isinstance(path, str)}
    if any(_worktree_blob(repository, path) != _git_blob(repository, evidence_commit, path) for path in checkpoint_paths):
        _error(errors, "WORKTREE_EVIDENCE_SUBSTITUTION", "worktree envelope or raw evidence differs from the evidence checkpoint tree")
    subject_parent = _commit_parent(repository, subject_commit)
    subject_task = _git_text(repository, subject_commit, task_path)
    subject_parent_task = _git_text(repository, subject_parent, task_path) if subject_parent else None
    if subject_task is None or subject_parent_task is None or _metadata(subject_task) is None or _metadata(subject_parent_task) is None or _metadata(subject_task)[0] != "InProgress" or _metadata(subject_parent_task)[0] != "Planned" or not _metadata(subject_task)[1].startswith("/"):
        _error(errors, "INVALID_SUBJECT_METADATA", "subject commit must move the active task from Planned to InProgress with a real assignee")
    owned_paths = _owned_surface_paths(subject_task) if subject_task else None
    artifact_paths = _validate_source_artifacts(envelope.get("artifacts"), repository, subject_commit, errors)
    expected_artifacts = {path for path in owned_paths or set() if not path.startswith("evidence/")}
    subject_changes = _changed_paths(repository, subject_parent, subject_commit) if subject_parent else set()
    if owned_paths is None or artifact_paths != expected_artifacts or subject_changes != expected_artifacts | {task_path}:
        _error(errors, "SUBJECT_ARTIFACT_SET_MISMATCH", "subject must change every and only its non-evidence owned artifacts plus task metadata")
    return {"valid": not errors, "errors": errors}


def validate_historical_v0_gov_035(repository: Path) -> dict[str, object]:
    """Reject the immutable V0-GOV-035 record whose hashes predate its closure."""
    errors: list[dict[str, str]] = []
    verification = repository / "evidence/V0-GOV-035/verification.md"
    try:
        text = verification.read_text(encoding="utf-8")
    except (FileNotFoundError, UnicodeDecodeError):
        return {"valid": False, "errors": [{"code": "MISSING_HISTORICAL_RECORD", "message": str(verification)}]}
    baseline_match = re.search(r"^- Baseline HEAD: `([0-9a-f]{40})`$", text, re.MULTILINE)
    recorded_hashes = re.findall(r"^([A-F0-9]{64})  ([^\r\n]+)$", text, re.MULTILINE)
    if baseline_match is None or baseline_match.group(1) != _V0_GOV_035_BASELINE or not recorded_hashes:
        _error(errors, "INVALID_HISTORICAL_RECORD", "V0-GOV-035 baseline or hash ledger is malformed")
        return {"valid": False, "errors": errors}
    if _commit_parent(repository, _V0_GOV_035_CLOSURE) != _V0_GOV_035_BASELINE:
        _error(errors, "INVALID_HISTORICAL_RECORD", "V0-GOV-035 closure no longer has the recorded baseline parent")
        return {"valid": False, "errors": errors}
    for expected, path in recorded_hashes:
        blob = _git_blob(repository, _V0_GOV_035_BASELINE, path)
        if blob is None or hashlib.sha256(blob).hexdigest().upper() != expected:
            _error(errors, "FINAL_BLOB_HASH_MISMATCH", f"historical baseline blob differs: {path}")
        if _git(repository, "diff", "--quiet", _V0_GOV_035_BASELINE, _V0_GOV_035_CLOSURE, "--", path).returncode != 0:
            _error(errors, "STALE_CANDIDATE_COMMIT", f"historical baseline predates closure artifact: {path}")
    return {"valid": not errors, "errors": errors}


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--envelope", type=Path)
    group.add_argument("--final-commit")
    group.add_argument("--historical-v0-gov-035", action="store_true")
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--format", choices=("text", "json"), default="text")
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    repository = args.repository.resolve()
    if args.final_commit:
        result = validate_final_commit(args.final_commit, repository)
    elif args.historical_v0_gov_035:
        result = validate_historical_v0_gov_035(repository)
    else:
        result = validate_envelope(args.envelope, repository)
    if args.format == "json":
        print(json.dumps(result, sort_keys=True))
    elif result["valid"]:
        print("OK: Closure evidence envelope is valid")
    else:
        for error in result["errors"]:
            print(f"{error['code']}: {error['message']}")
    return 0 if result["valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
