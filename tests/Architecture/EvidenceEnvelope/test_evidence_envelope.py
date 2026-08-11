from __future__ import annotations

import hashlib
import importlib.util
import json
import subprocess
from pathlib import Path

import pytest


TOOL = Path(__file__).resolve().parents[3] / "tools" / "evidence-envelope" / "evidence_envelope_tool.py"


@pytest.fixture()
def tool_module():
    spec = importlib.util.spec_from_file_location("evidence_envelope_tool", TOOL)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def _commit(repo: Path, message: str) -> str:
    _git(repo, "add", ".")
    _git(repo, "commit", "-q", "-m", message)
    return _git(repo, "rev-parse", "HEAD")


@pytest.fixture()
def repository(tmp_path: Path) -> tuple[Path, str]:
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Evidence Test")
    (repo / "src").mkdir()
    (repo / "src" / "candidate.txt").write_text("candidate blob\n", encoding="utf-8")
    candidate = _commit(repo, "candidate")
    return repo, candidate


def _hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _candidate_blob_hash(repo: Path, candidate: str, path: str) -> str:
    blob = subprocess.run(
        ["git", "-C", str(repo), "show", f"{candidate}:{path}"],
        check=True,
        capture_output=True,
    ).stdout
    return hashlib.sha256(blob).hexdigest()


def _write_envelope(repo: Path, candidate: str, tool_module, **overrides: object) -> Path:
    raw = repo / "evidence" / "V0-GOV-039" / "raw" / "pytest.txt"
    raw.parent.mkdir(parents=True, exist_ok=True)
    raw.write_text("1 passed in 0.01s\n", encoding="utf-8")
    envelope: dict[str, object] = {
        "schema": tool_module.SCHEMA,
        "task_id": "V0-GOV-039",
        "subject_commit": candidate,
        "environment": {
            "platform": "Windows 10",
            "toolchain": {"python": "3.12.12"},
            "variables": {"CI": "false"},
            "secrets": [
                {
                    "location": "env:ALKAROS_TEST_PG_PASSWORD",
                    "fingerprint": "sha256:" + ("a" * 64),
                }
            ],
        },
        "commands": [
            {
                "command": "py -m pytest tests/Architecture/EvidenceEnvelope -q",
                "exit_code": 0,
                "raw_output": {"path": "evidence/V0-GOV-039/raw/pytest.txt", "sha256": _hash(raw)},
            }
        ],
        "artifacts": [
            {
                "path": "src/candidate.txt",
                "sha256": _candidate_blob_hash(repo, candidate, "src/candidate.txt"),
            }
        ],
    }
    envelope.update(overrides)
    envelope["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(envelope)}
    path = repo / "evidence" / "V0-GOV-039" / "closure-evidence-envelope.json"
    path.write_text(json.dumps(envelope, indent=2) + "\n", encoding="utf-8")
    return path


def _error_codes(result: dict[str, object]) -> set[str]:
    errors = result["errors"]
    assert isinstance(errors, list)
    return {item["code"] for item in errors}


def _write_closure_chain(repo: Path, tool_module) -> tuple[str, str, str]:
    task_path = "plan/v0/governance/V0-GOV-049.md"
    owned_paths = [
        "tools/evidence-envelope/evidence_envelope_tool.py",
        "tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py",
        "docs/engineering/closure-evidence-envelope.md",
        "plan/VALIDATION_CONTRACT.md",
    ]
    task = repo / task_path
    task.parent.mkdir(parents=True)
    task.write_text(
        "# V0-GOV-049\n\n"
        "- Task ID: V0-GOV-049\n"
        "- Status: Planned\n"
        "- Assignee: Unassigned (exactly one person)\n\n"
        "## Owned surface\n\n"
        + "".join(f"- `{path}`\n" for path in owned_paths)
        + "- `evidence/V0-GOV-049/**`\n\n## In scope\n",
        encoding="utf-8",
    )
    _commit(repo, "add task")
    for path in owned_paths:
        artifact = repo / path
        artifact.parent.mkdir(parents=True, exist_ok=True)
        artifact.write_text(f"{path}\n", encoding="utf-8")
    task.write_text(task.read_text(encoding="utf-8").replace("Status: Planned", "Status: InProgress").replace("Assignee: Unassigned (exactly one person)", "Assignee: /root/test"), encoding="utf-8")
    subject = _commit(repo, "subject")
    raw = repo / "evidence" / "V0-GOV-049" / "raw" / "pytest.txt"
    raw.parent.mkdir(parents=True)
    raw.write_text("1 passed\n", encoding="utf-8")
    envelope = {
        "schema": tool_module.SCHEMA,
        "task_id": "V0-GOV-049",
        "subject_commit": subject,
        "environment": {"platform": "Windows", "toolchain": {"python": "3.12"}, "variables": {}, "secrets": []},
        "commands": [{"command": "py -m pytest tests/Architecture/EvidenceEnvelope -q", "exit_code": 0, "raw_output": {"path": "evidence/V0-GOV-049/raw/pytest.txt", "sha256": _hash(raw)}}],
        "artifacts": [{"path": path, "sha256": _candidate_blob_hash(repo, subject, path)} for path in owned_paths],
    }
    envelope["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(envelope)}
    envelope_path = repo / "evidence" / "V0-GOV-049" / "closure-evidence-envelope.json"
    envelope_path.write_text(json.dumps(envelope, indent=2) + "\n", encoding="utf-8")
    evidence = _commit(repo, "evidence")
    task.write_text(task.read_text(encoding="utf-8").replace("Status: InProgress", "Status: Done"), encoding="utf-8")
    final = _commit(
        repo,
        "close\n\nTask: V0-GOV-049\nGate: GATE-V0-EXIT\n"
        f"Closure-Subject: {subject}\nClosure-Evidence-Checkpoint: {evidence}",
    )
    return subject, evidence, final


def test_valid_envelope_is_accepted(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)

    assert tool_module.validate_envelope(envelope, repo) == {"valid": True, "errors": []}


def test_final_commit_requires_v2_subject_evidence_final_chain(repository, tool_module):
    repo, _ = repository
    _, _, final = _write_closure_chain(repo, tool_module)

    assert tool_module.validate_final_commit(final, repo) == {"valid": True, "errors": []}


def test_immutable_v0_gov_035_hash_ledger_is_invalid(tool_module):
    repository = TOOL.parents[2]

    errors = _error_codes(tool_module.validate_historical_v0_gov_035(repository))
    assert {"STALE_CANDIDATE_COMMIT", "FINAL_BLOB_HASH_MISMATCH"} <= errors


def test_missing_exit_code_fails_closed(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    del payload["commands"][0]["exit_code"]
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    assert "MISSING_EXIT_CODE" in _error_codes(tool_module.validate_envelope(envelope, repo))


def test_v0_gov_035_style_preclose_task_scope_claim_fails_closed(repository, tool_module):
    repo, candidate = repository
    (repo / "src" / "candidate.txt").write_text("newer final blob\n", encoding="utf-8")
    final_commit = _commit(repo, "change candidate")
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["commands"][0]["command"] = (
        "py -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-035 --format text"
    )
    payload["artifacts"][0]["sha256"] = _candidate_blob_hash(repo, final_commit, "src/candidate.txt")
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    errors = _error_codes(tool_module.validate_envelope(envelope, repo))
    assert {"STALE_CANDIDATE_COMMIT", "FINAL_BLOB_HASH_MISMATCH"} <= errors


def test_final_blob_hash_mismatch_fails_closed(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["artifacts"][0]["sha256"] = "b" * 64
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    assert "FINAL_BLOB_HASH_MISMATCH" in _error_codes(tool_module.validate_envelope(envelope, repo))


def test_secret_leakage_fails_closed(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["environment"]["variables"]["DATABASE_PASSWORD"] = "not-redacted"
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    assert "SECRET_LEAKAGE" in _error_codes(tool_module.validate_envelope(envelope, repo))


def test_raw_secret_assignment_fails_closed(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    raw = repo / "evidence" / "V0-GOV-039" / "raw" / "pytest.txt"
    raw.write_text("password=not-redacted\n", encoding="utf-8")

    assert "SECRET_LEAKAGE" in _error_codes(tool_module.validate_envelope(envelope, repo))


@pytest.mark.parametrize(
    "secret_text",
    [
        "Authorization: Bearer leaked-value",
        "api key: leaked-value",
    ],
)
def test_raw_bearer_and_api_key_leakage_fails_closed(repository, tool_module, secret_text):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    raw = repo / "evidence" / "V0-GOV-039" / "raw" / "pytest.txt"
    raw.write_text(secret_text + "\n", encoding="utf-8")

    assert "SECRET_LEAKAGE" in _error_codes(tool_module.validate_envelope(envelope, repo))


@pytest.mark.parametrize(
    "secret_text",
    [
        "Authorization: Bearer leaked-value",
        "api key: leaked-value",
    ],
)
def test_command_bearer_and_api_key_leakage_fails_closed(repository, tool_module, secret_text):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["commands"][0]["command"] = secret_text
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    assert "SECRET_LEAKAGE" in _error_codes(tool_module.validate_envelope(envelope, repo))


def test_narrative_only_evidence_fails_closed(repository, tool_module):
    repo, _ = repository
    envelope = repo / "evidence" / "V0-GOV-039" / "closure-evidence-envelope.json"
    envelope.parent.mkdir(parents=True)
    envelope.write_text(json.dumps({"narrative": "all checks passed"}), encoding="utf-8")

    assert "NARRATIVE_ONLY_EVIDENCE" in _error_codes(tool_module.validate_envelope(envelope, repo))


def test_integrity_hash_tampering_fails_closed(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["commands"][0]["command"] = "py -m pytest altered"
    envelope.write_text(json.dumps(payload), encoding="utf-8")

    assert "INTEGRITY_HASH_MISMATCH" in _error_codes(tool_module.validate_envelope(envelope, repo))
