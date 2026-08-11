from __future__ import annotations

import hashlib
import importlib.util
import json
import subprocess
from collections.abc import Callable
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
    raw.write_bytes(b"1 passed in 0.01s\n")
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


def _write_closure_chain(
    repo: Path,
    tool_module,
    omitted_artifact: str | None = None,
) -> tuple[str, str, str]:
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
    raw.write_bytes(b"1 passed\n")
    envelope = {
        "schema": tool_module.SCHEMA,
        "task_id": "V0-GOV-049",
        "subject_commit": subject,
        "environment": {"platform": "Windows", "toolchain": {"python": "3.12"}, "variables": {}, "secrets": []},
        "commands": [{"command": "py -m pytest tests/Architecture/EvidenceEnvelope -q", "exit_code": 0, "raw_output": {"path": "evidence/V0-GOV-049/raw/pytest.txt", "sha256": _hash(raw)}}],
        "artifacts": [
            {"path": path, "sha256": _candidate_blob_hash(repo, subject, path)}
            for path in owned_paths
            if path != omitted_artifact
        ],
    }
    envelope["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(envelope)}
    envelope_path = repo / "evidence" / "V0-GOV-049" / "closure-evidence-envelope.json"
    envelope_path.write_bytes((json.dumps(envelope, indent=2) + "\n").encode("utf-8"))
    evidence = _commit(repo, "evidence")
    task.write_text(task.read_text(encoding="utf-8").replace("Status: InProgress", "Status: Done"), encoding="utf-8")
    final = _commit(
        repo,
        "close\n\nTask: V0-GOV-049\nGate: GATE-V0-EXIT\n"
        f"Closure-Subject: {subject}\nClosure-Evidence-Checkpoint: {evidence}",
    )
    return subject, evidence, final


def _write_v3_interrupted_chain(
    repo: Path,
    tool_module,
    monkeypatch,
    mutate: Callable[[str, Path], None] | None = None,
) -> tuple[str, str, str, str, str]:
    fnd_task_path = "plan/v1/foundation/V1-FND-023-solution-test-discovery.md"
    v055_task_path = "plan/v0/governance/V0-GOV-055.md"
    fnd_task = repo / fnd_task_path
    v055_task = repo / v055_task_path
    source_test_path = "tests/Architecture/TestDiscovery/test_solution_test_discovery.py"
    source_paths = ("Directory.Build.targets", source_test_path)
    (repo / "Directory.Build.targets").write_text("before\n", encoding="utf-8")
    fnd_task.parent.mkdir(parents=True)
    fnd_task.write_text(
        "# V1-FND-023\n\n- Task ID: V1-FND-023\n- Status: Planned\n"
        "- Assignee: Unassigned (exactly one person)\n\n## Owned surface\n\n"
        "- `Directory.Build.targets`\n"
        "- `tests/Architecture/TestDiscovery/test_solution_test_discovery.py`\n"
        "- `evidence/V1-FND-023/**`\n\n## Deliverables\n",
        encoding="utf-8",
    )
    v055_task.parent.mkdir(parents=True)
    v055_owned_paths = (
        "tools/evidence-envelope/evidence_envelope_tool.py",
        "tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py",
        "docs/engineering/closure-evidence-envelope.md",
        "tools/plan-audit/plan_audit_tool.py",
        "tests/Architecture/PlanAudit/test_plan_audit.py",
        "plan/VALIDATION_CONTRACT.md",
    )
    v055_task.write_text(
        "# V0-GOV-055\n\n- Task ID: V0-GOV-055\n- Status: Planned\n"
        "- Assignee: Unassigned (exactly one person)\n\n## Owned surface\n\n"
        + "".join(f"- `{path}`\n" for path in v055_owned_paths)
        + "- `evidence/V0-GOV-055/**`\n\n## Deliverables\n",
        encoding="utf-8",
    )
    b0_parent = _commit(repo, "initial")
    (repo / "Directory.Build.targets").write_text("restored\n", encoding="utf-8")
    source_test = repo / source_test_path
    source_test.parent.mkdir(parents=True)
    source_test.write_text("test source\n", encoding="utf-8")
    fnd_task.write_text(
        fnd_task.read_text(encoding="utf-8")
        .replace("Status: Planned", "Status: InProgress")
        .replace("Assignee: Unassigned (exactly one person)", "Assignee: /root/implement_v1_fnd_023"),
        encoding="utf-8",
    )
    if mutate is not None:
        mutate("b0", repo)
    b0 = _commit(repo, "B0")
    if mutate is not None:
        mutate("before_interruption", repo)
    fnd_task.write_text(
        fnd_task.read_text(encoding="utf-8")
        .replace("Status: InProgress", "Status: Blocked")
        .replace("\n## Deliverables\n", f"\n{tool_module._V3_INTERRUPTION_BLOCKER}\n## Deliverables\n"),
        encoding="utf-8",
    )
    if mutate is not None:
        mutate("interruption", repo)
    interruption = _commit(repo, "interruption")

    for path in v055_owned_paths:
        artifact = repo / path
        artifact.parent.mkdir(parents=True, exist_ok=True)
        artifact.write_text(f"{path}\n", encoding="utf-8")
    v055_task.write_text(
        v055_task.read_text(encoding="utf-8")
        .replace("Status: Planned", "Status: InProgress")
        .replace("Assignee: Unassigned (exactly one person)", "Assignee: /root/v055"),
        encoding="utf-8",
    )
    v055_subject = _commit(repo, "V055 subject")
    v055_raw = repo / "evidence/V0-GOV-055/raw/pytest.txt"
    v055_raw.parent.mkdir(parents=True)
    v055_raw.write_bytes(b"1 passed\n")
    v055_envelope = {
        "schema": tool_module.SCHEMA,
        "task_id": "V0-GOV-055",
        "subject_commit": v055_subject,
        "environment": {"platform": "Windows", "toolchain": {"python": "3.12"}, "variables": {}, "secrets": []},
        "commands": [{"command": "py -m pytest", "exit_code": 0, "raw_output": {"path": "evidence/V0-GOV-055/raw/pytest.txt", "sha256": _hash(v055_raw)}}],
        "artifacts": [{"path": path, "sha256": _candidate_blob_hash(repo, v055_subject, path)} for path in v055_owned_paths],
    }
    v055_envelope["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(v055_envelope)}
    (repo / "evidence/V0-GOV-055/closure-evidence-envelope.json").write_text(json.dumps(v055_envelope), encoding="utf-8")
    v055_evidence = _commit(repo, "V055 evidence")
    v055_task.write_text(v055_task.read_text(encoding="utf-8").replace("Status: InProgress", "Status: Done"), encoding="utf-8")
    v055_final = _commit(repo, f"V055 final\n\nTask: V0-GOV-055\nGate: GATE-V0-EXIT\nClosure-Subject: {v055_subject}\nClosure-Evidence-Checkpoint: {v055_evidence}")

    reentry_text = tool_module._task_without_blocker(fnd_task.read_text(encoding="utf-8"))
    assert reentry_text is not None
    fnd_task.write_text(reentry_text.replace("Status: Blocked", "Status: InProgress"), encoding="utf-8")
    if mutate is not None:
        mutate("reentry", repo)
    reentry = _commit(repo, "reentry")
    raw = repo / "evidence/V1-FND-023/raw/acceptance.txt"
    raw.parent.mkdir(parents=True)
    raw.write_bytes(b"acceptance passed\n")
    envelope = {
        "schema": tool_module.SCHEMA,
        "task_id": "V1-FND-023",
        "subject_commit": b0,
        "environment": {"platform": "Windows", "toolchain": {"python": "3.12"}, "variables": {}, "secrets": []},
        "commands": [{"command": "py -m pytest", "exit_code": 0, "raw_output": {"path": "evidence/V1-FND-023/raw/acceptance.txt", "sha256": _hash(raw)}}],
        "artifacts": [{"path": path, "sha256": _candidate_blob_hash(repo, b0, path)} for path in source_paths],
    }
    envelope["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(envelope)}
    (repo / "evidence/V1-FND-023/closure-evidence-envelope.json").write_text(json.dumps(envelope), encoding="utf-8")
    if mutate is not None:
        mutate("evidence", repo)
    evidence = _commit(repo, "V1 evidence")
    fnd_task.write_text(fnd_task.read_text(encoding="utf-8").replace("Status: InProgress", "Status: Done"), encoding="utf-8")
    if mutate is not None:
        mutate("final", repo)
    final = _commit(repo, f"V1 final\n\nTask: V1-FND-023\nGate: GATE-V0-EXIT\nClosure-Subject: {b0}\nClosure-Interruption: {interruption}\nClosure-Reentry: {reentry}\nClosure-Evidence-Checkpoint: {evidence}")

    monkeypatch.setattr(tool_module, "_V3_B0_PARENT", b0_parent)
    monkeypatch.setattr(tool_module, "_V3_B0_COMMIT", b0)
    monkeypatch.setattr(tool_module, "_V3_INTERRUPTION_COMMIT", interruption)
    monkeypatch.setattr(tool_module, "_V3_SOURCE_ARTIFACTS", {path: _candidate_blob_hash(repo, b0, path) for path in source_paths})
    return b0, interruption, reentry, evidence, final


def test_valid_envelope_is_accepted(repository, tool_module):
    repo, candidate = repository
    envelope = _write_envelope(repo, candidate, tool_module)

    assert tool_module.validate_envelope(envelope, repo) == {"valid": True, "errors": []}


def test_final_commit_requires_v2_subject_evidence_final_chain(repository, tool_module):
    repo, _ = repository
    _, _, final = _write_closure_chain(repo, tool_module)

    assert tool_module.validate_final_commit(final, repo) == {"valid": True, "errors": []}


def test_v3_interrupted_fnd023_closure_is_accepted(repository, tool_module, monkeypatch):
    repo, _ = repository
    _, _, _, _, final = _write_v3_interrupted_chain(repo, tool_module, monkeypatch)

    assert tool_module.validate_final_commit(final, repo) == {"valid": True, "errors": []}


def test_v3_task_specific_api_rejects_a_valid_generic_v2_final(repository, tool_module):
    repo, _ = repository
    _, _, final = _write_closure_chain(repo, tool_module)

    assert tool_module.validate_final_commit(final, repo) == {"valid": True, "errors": []}
    assert "V3_INVALID_TOPOLOGY" in _error_codes(
        tool_module.validate_v1_fnd_023_v3_final_commit(final, repo)
    )


def test_v3_interrupted_fnd023_rejects_worktree_evidence_substitution(repository, tool_module, monkeypatch):
    repo, _ = repository
    _, _, _, _, final = _write_v3_interrupted_chain(repo, tool_module, monkeypatch)
    (repo / "evidence/V1-FND-023/raw/acceptance.txt").write_bytes(b"forged\n")

    assert "WORKTREE_EVIDENCE_SUBSTITUTION" in _error_codes(tool_module.validate_final_commit(final, repo))


def test_v3_interrupted_fnd023_rejects_extra_final_trailer(repository, tool_module, monkeypatch):
    repo, _ = repository
    _, _, _, _, final = _write_v3_interrupted_chain(repo, tool_module, monkeypatch)
    _git(repo, "commit", "--amend", "-q", "-m", _git(repo, "show", "-s", "--format=%B", final) + "Extra: trailer")
    amended = _git(repo, "rev-parse", "HEAD")

    assert "V3_INVALID_FINAL_TRAILERS" in _error_codes(tool_module.validate_final_commit(amended, repo))


def test_v3_interrupted_fnd023_rejects_wrong_b0_source_hash(repository, tool_module, monkeypatch):
    repo, _ = repository
    _write_v3_interrupted_chain(repo, tool_module, monkeypatch)
    monkeypatch.setattr(tool_module, "_V3_SOURCE_ARTIFACTS", {"Directory.Build.targets": "0" * 64})

    assert "V3_B0_BLOB_MISMATCH" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


def test_v3_interrupted_fnd023_rejects_non_direct_interruption(repository, tool_module, monkeypatch):
    repo, _ = repository

    def mutate(stage: str, fixture: Path) -> None:
        if stage == "before_interruption":
            (fixture / "interloper.txt").write_text("break adjacency\n", encoding="utf-8")
            _commit(fixture, "interloper")

    _write_v3_interrupted_chain(repo, tool_module, monkeypatch, mutate)

    assert "V3_INVALID_INTERRUPTION" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


def test_v3_interrupted_fnd023_rejects_changed_blocker_text(repository, tool_module, monkeypatch):
    repo, _ = repository

    def mutate(stage: str, fixture: Path) -> None:
        if stage == "interruption":
            task = fixture / "plan/v1/foundation/V1-FND-023-solution-test-discovery.md"
            task.write_text(
                task.read_text(encoding="utf-8").replace("V0-GOV-054", "V0-GOV-999"),
                encoding="utf-8",
            )

    _write_v3_interrupted_chain(repo, tool_module, monkeypatch, mutate)

    assert "V3_INVALID_INTERRUPTION_DIFF" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


@pytest.mark.parametrize(
    ("stage", "path", "expected"),
    [
        ("reentry", "reentry-extra.txt", "V3_INVALID_REENTRY_DIFF"),
        ("evidence", "evidence-extra.txt", "V3_EVIDENCE_NOT_CHECKPOINT_ONLY"),
        ("final", "final-extra.txt", "V3_FINAL_NOT_METADATA_ONLY"),
    ],
    ids=["a-path", "e-path", "f-path"],
)
def test_v3_interrupted_fnd023_rejects_extra_changed_path(
    repository, tool_module, monkeypatch, stage, path, expected
):
    repo, _ = repository

    def mutate(current_stage: str, fixture: Path) -> None:
        if current_stage == stage:
            (fixture / path).write_text("out of contract\n", encoding="utf-8")

    _write_v3_interrupted_chain(repo, tool_module, monkeypatch, mutate)

    assert expected in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


@pytest.mark.parametrize(
    ("mutation", "expected"),
    [
        (
            lambda repo: (repo / "evidence/V1-FND-023/raw/acceptance.txt").unlink(),
            "V3_EVIDENCE_RAW_NOT_CHECKPOINTED",
        ),
        (
            lambda repo: (repo / "evidence/V1-FND-023/closure-evidence-envelope.json").unlink(),
            "V3_INVALID_CLOSURE_ENVELOPE",
        ),
    ],
    ids=["missing-raw", "missing-envelope"],
)
def test_v3_interrupted_fnd023_rejects_missing_checkpoint_artifact(
    repository, tool_module, monkeypatch, mutation, expected
):
    repo, _ = repository

    def mutate(stage: str, fixture: Path) -> None:
        if stage == "evidence":
            mutation(fixture)

    _write_v3_interrupted_chain(repo, tool_module, monkeypatch, mutate)

    assert expected in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


def test_v3_interrupted_fnd023_rejects_tampered_checkpoint_hash(repository, tool_module, monkeypatch):
    repo, _ = repository

    def mutate(stage: str, fixture: Path) -> None:
        if stage == "evidence":
            envelope = fixture / "evidence/V1-FND-023/closure-evidence-envelope.json"
            payload = json.loads(envelope.read_text(encoding="utf-8"))
            payload["commands"][0]["raw_output"]["sha256"] = "0" * 64
            envelope.write_text(json.dumps(payload), encoding="utf-8")

    _write_v3_interrupted_chain(repo, tool_module, monkeypatch, mutate)

    assert "INTEGRITY_HASH_MISMATCH" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


@pytest.mark.parametrize(
    ("message_transform", "expected"),
    [
        (lambda lines: lines[:-1], "V3_INVALID_FINAL_TRAILERS"),
        (lambda lines: [lines[1], lines[0], *lines[2:]], "V3_INVALID_FINAL_TRAILERS"),
    ],
    ids=["missing-trailer", "misordered-trailer"],
)
def test_v3_interrupted_fnd023_rejects_missing_or_misordered_trailer(
    repository, tool_module, monkeypatch, message_transform, expected
):
    repo, _ = repository
    _write_v3_interrupted_chain(repo, tool_module, monkeypatch)
    message = _git(repo, "show", "-s", "--format=%B", "HEAD").splitlines()
    _git(repo, "commit", "--amend", "-q", "-m", "\n".join(message_transform(message)))

    assert expected in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(_git(repo, "rev-parse", "HEAD"), repo)
    )


def test_v3_interrupted_fnd023_rejects_other_task_and_non_final_head(repository, tool_module, monkeypatch):
    repo, _ = repository
    _, _, _, evidence, final = _write_v3_interrupted_chain(repo, tool_module, monkeypatch)
    message = _git(repo, "show", "-s", "--format=%B", final).replace("Task: V1-FND-023", "Task: V1-FND-022")
    _git(repo, "commit", "--amend", "-q", "-m", message)
    wrong_task = _git(repo, "rev-parse", "HEAD")

    assert "V3_INVALID_FINAL_TRAILERS" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(wrong_task, repo)
    )
    assert "V3_REENTRY_PARENT_NOT_V055_FINAL" in _error_codes(
        tool_module.validate_v3_interrupted_final_commit(evidence, repo)
    )


def test_final_commit_rejects_worktree_evidence_substitution(repository, tool_module):
    repo, _ = repository
    _, _, final = _write_closure_chain(repo, tool_module)
    raw = repo / "evidence" / "V0-GOV-049" / "raw" / "pytest.txt"
    envelope = repo / "evidence" / "V0-GOV-049" / "closure-evidence-envelope.json"
    raw.write_bytes(b"forged success\n")
    payload = json.loads(envelope.read_text(encoding="utf-8"))
    payload["commands"][0]["raw_output"]["sha256"] = _hash(raw)
    payload["integrity"] = {"payload_sha256": tool_module.canonical_payload_hash(payload)}
    envelope.write_bytes((json.dumps(payload, indent=2) + "\n").encode("utf-8"))

    errors = _error_codes(tool_module.validate_final_commit(final, repo))
    assert errors == {"WORKTREE_EVIDENCE_SUBSTITUTION"}


def test_final_commit_rejects_missing_owned_subject_artifact(repository, tool_module):
    repo, _ = repository
    _, _, final = _write_closure_chain(
        repo,
        tool_module,
        omitted_artifact="docs/engineering/closure-evidence-envelope.md",
    )

    assert "SUBJECT_ARTIFACT_SET_MISMATCH" in _error_codes(tool_module.validate_final_commit(final, repo))


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
