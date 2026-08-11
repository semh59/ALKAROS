from __future__ import annotations

"""Validate tamper-evident, replayable task-closure evidence envelopes."""

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any


SCHEMA = "alkaros.closure-evidence-envelope/v1"
_TASK_ID = re.compile(r"^V\d+-[A-Z]+-\d+$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_GIT_COMMIT = re.compile(r"^[0-9a-f]{40,64}$")
_SAFE_RELATIVE_PATH = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._/-]*$")
_SECRET_NAME = re.compile(r"(?:password|secret|token|api[_-]?key)", re.IGNORECASE)
_SECRET_ASSIGNMENT = re.compile(
    r"\b(?:password|secret|token|api[_-]?key)\b\s*(?:=|:)\s*[^\s]+",
    re.IGNORECASE,
)
_SECRET_FLAG = re.compile(
    r"--(?:password|secret|token|api[_-]?key)\b(?:\s+|=)\S+",
    re.IGNORECASE,
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


def _git(repo: Path, *args: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        check=False,
    )


def _git_commit_exists(repo: Path, commit: str) -> bool:
    return _git(repo, "rev-parse", "--verify", f"{commit}^{{commit}}").returncode == 0


def _git_blob(repo: Path, commit: str, path: str) -> bytes | None:
    result = _git(repo, "show", f"{commit}:{path}")
    return result.stdout if result.returncode == 0 else None


def _is_stale_candidate(repo: Path, candidate_commit: str, path: str) -> bool:
    head = _git(repo, "rev-parse", "--verify", "HEAD^{commit}")
    if head.returncode != 0:
        return True
    if _git(repo, "merge-base", "--is-ancestor", candidate_commit, "HEAD").returncode != 0:
        return True
    return _git(repo, "diff", "--quiet", f"{candidate_commit}..HEAD", "--", path).returncode != 0


def _validate_environment(value: object, errors: list[dict[str, str]]) -> None:
    if not isinstance(value, dict):
        _error(errors, "MISSING_ENVIRONMENT", "environment must be an object")
        return
    if set(value) != {"platform", "toolchain", "variables", "secrets"}:
        _error(
            errors,
            "INVALID_ENVIRONMENT",
            "environment must contain only platform, toolchain, variables, and secrets",
        )
        return
    if not isinstance(value["platform"], str) or not value["platform"].strip():
        _error(errors, "INVALID_ENVIRONMENT", "environment.platform is required")
    if not isinstance(value["toolchain"], dict) or not value["toolchain"]:
        _error(errors, "INVALID_ENVIRONMENT", "environment.toolchain must be a non-empty object")
    else:
        for name, item in value["toolchain"].items():
            if not isinstance(name, str) or not isinstance(item, str) or _SECRET_NAME.search(name):
                _error(
                    errors,
                    "SECRET_LEAKAGE",
                    "environment.toolchain may not contain a sensitive name or non-string value",
                )
    if not isinstance(value["variables"], dict):
        _error(errors, "INVALID_ENVIRONMENT", "environment.variables must be an object")
    else:
        for name, item in value["variables"].items():
            if not isinstance(name, str) or not isinstance(item, str) or _SECRET_NAME.search(name):
                _error(
                    errors,
                    "SECRET_LEAKAGE",
                    "environment.variables may not contain a sensitive name or non-string value",
                )
    if not isinstance(value["secrets"], list):
        _error(errors, "INVALID_ENVIRONMENT", "environment.secrets must be a list")
        return
    for index, secret in enumerate(value["secrets"]):
        if not isinstance(secret, dict) or set(secret) != {"location", "fingerprint"}:
            _error(
                errors,
                "SECRET_LEAKAGE",
                f"environment.secrets[{index}] must contain only location and fingerprint",
            )
            continue
        location = secret["location"]
        fingerprint = secret["fingerprint"]
        if not isinstance(location, str) or not location.startswith("env:"):
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}].location must be redacted")
        if not isinstance(fingerprint, str) or not fingerprint.startswith("sha256:"):
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}].fingerprint must be SHA-256")
        elif not _SHA256.fullmatch(fingerprint.removeprefix("sha256:")):
            _error(errors, "SECRET_LEAKAGE", f"environment.secrets[{index}].fingerprint is invalid")


def _validate_raw_output(
    value: object,
    repo: Path,
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
    artifact = repo / path
    if not artifact.is_file():
        _error(errors, "MISSING_RAW_OUTPUT", f"{field}.path is missing: {path}")
        return
    content = artifact.read_bytes()
    actual_hash = hashlib.sha256(content).hexdigest()
    if actual_hash != expected_hash:
        _error(errors, "RAW_OUTPUT_HASH_MISMATCH", f"{field}.path hash does not match")
    text = content.decode("utf-8", errors="replace")
    if _SECRET_ASSIGNMENT.search(text):
        _error(errors, "SECRET_LEAKAGE", f"{field}.path contains a secret assignment")


def _validate_commands(value: object, repo: Path, task_id: str, errors: list[dict[str, str]]) -> None:
    if not isinstance(value, list) or not value:
        _error(errors, "MISSING_COMMAND", "commands must contain at least one command record")
        return
    for index, command in enumerate(value):
        field = f"commands[{index}]"
        if not isinstance(command, dict) or set(command) != {"command", "exit_code", "raw_output"}:
            if isinstance(command, dict) and "exit_code" not in command:
                _error(errors, "MISSING_EXIT_CODE", f"{field}.exit_code is required")
            else:
                _error(errors, "INVALID_COMMAND", f"{field} must contain command, exit_code, and raw_output")
            continue
        command_text = command["command"]
        if not isinstance(command_text, str) or not command_text.strip():
            _error(errors, "MISSING_COMMAND", f"{field}.command is required")
        elif _SECRET_ASSIGNMENT.search(command_text) or _SECRET_FLAG.search(command_text):
            _error(errors, "SECRET_LEAKAGE", f"{field}.command contains a secret assignment")
        exit_code = command["exit_code"]
        if type(exit_code) is not int:
            _error(errors, "MISSING_EXIT_CODE", f"{field}.exit_code must be an integer")
        elif exit_code != 0:
            _error(errors, "NONZERO_EXIT_CODE", f"{field}.exit_code must be 0 for closure evidence")
        _validate_raw_output(command["raw_output"], repo, task_id, errors, f"{field}.raw_output")


def _validate_source_artifacts(
    value: object,
    repo: Path,
    candidate_commit: str | None,
    errors: list[dict[str, str]],
) -> None:
    if not isinstance(value, list) or not value:
        _error(errors, "MISSING_ARTIFACT_HASH", "artifacts must contain at least one source blob")
        return
    seen_paths: set[str] = set()
    for index, artifact in enumerate(value):
        field = f"artifacts[{index}]"
        if not isinstance(artifact, dict) or set(artifact) != {"path", "sha256"}:
            _error(errors, "INVALID_ARTIFACT", f"{field} must contain path and sha256")
            continue
        path = _safe_path(artifact["path"], errors, f"{field}.path")
        expected_hash = _sha256(artifact["sha256"], errors, f"{field}.sha256")
        if path is None or expected_hash is None:
            continue
        if path in seen_paths:
            _error(errors, "DUPLICATE_ARTIFACT", f"{field}.path is duplicated: {path}")
            continue
        seen_paths.add(path)
        if candidate_commit is None:
            continue
        blob = _git_blob(repo, candidate_commit, path)
        if blob is None:
            _error(
                errors,
                "FINAL_BLOB_HASH_MISMATCH",
                f"candidate commit does not contain artifact: {path}",
            )
        elif hashlib.sha256(blob).hexdigest() != expected_hash:
            _error(errors, "FINAL_BLOB_HASH_MISMATCH", f"candidate blob hash differs: {path}")
        if _is_stale_candidate(repo, candidate_commit, path):
            _error(errors, "STALE_CANDIDATE_COMMIT", f"candidate commit is stale for artifact: {path}")


def validate_envelope(envelope_path: Path, repository: Path) -> dict[str, object]:
    """Return a deterministic validation result for one closure-evidence envelope."""
    errors: list[dict[str, str]] = []
    try:
        envelope = json.loads(envelope_path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return {"valid": False, "errors": [{"code": "MISSING_ENVELOPE", "message": str(envelope_path)}]}
    except UnicodeDecodeError:
        return {"valid": False, "errors": [{"code": "INVALID_ENVELOPE", "message": "envelope must be UTF-8 JSON"}]}
    except json.JSONDecodeError as exc:
        return {"valid": False, "errors": [{"code": "INVALID_ENVELOPE", "message": str(exc)}]}

    if not isinstance(envelope, dict):
        return {"valid": False, "errors": [{"code": "INVALID_ENVELOPE", "message": "root must be an object"}]}
    required = {"schema", "task_id", "candidate_commit", "environment", "commands", "artifacts", "integrity"}
    if set(envelope) != required:
        _error(errors, "NARRATIVE_ONLY_EVIDENCE", "envelope must contain exactly the closure schema fields")
        return {"valid": False, "errors": errors}
    if envelope["schema"] != SCHEMA:
        _error(errors, "INVALID_SCHEMA", f"schema must equal {SCHEMA}")
    task_id = envelope["task_id"]
    if not isinstance(task_id, str) or not _TASK_ID.fullmatch(task_id):
        _error(errors, "INVALID_TASK_ID", "task_id must use the canonical task ID format")
        task_id = "INVALID"
    candidate_commit = envelope["candidate_commit"]
    if not isinstance(candidate_commit, str) or not _GIT_COMMIT.fullmatch(candidate_commit):
        _error(errors, "MISSING_CANDIDATE_COMMIT", "candidate_commit must be a full Git commit hash")
        candidate_commit = None
    elif not _git_commit_exists(repository, candidate_commit):
        _error(errors, "STALE_CANDIDATE_COMMIT", "candidate_commit does not resolve to a commit")
        candidate_commit = None
    _validate_environment(envelope["environment"], errors)
    _validate_commands(envelope["commands"], repository, task_id, errors)
    _validate_source_artifacts(envelope["artifacts"], repository, candidate_commit, errors)

    integrity = envelope["integrity"]
    if not isinstance(integrity, dict) or set(integrity) != {"payload_sha256"}:
        _error(errors, "INVALID_INTEGRITY", "integrity must contain only payload_sha256")
    elif integrity["payload_sha256"] != canonical_payload_hash(envelope):
        _error(errors, "INTEGRITY_HASH_MISMATCH", "integrity.payload_sha256 does not match the envelope")

    return {"valid": not errors, "errors": errors}


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--envelope", required=True, type=Path)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--format", choices=("text", "json"), default="text")
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    result = validate_envelope(args.envelope, args.repository.resolve())
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
