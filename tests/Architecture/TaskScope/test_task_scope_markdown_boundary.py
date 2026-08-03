from __future__ import annotations

"""Fail-closed tests for task Markdown and entry-gate enforcement."""

import importlib.util
import subprocess
from pathlib import Path

import pytest


TOOL_PATH = Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py"


def _tool():
    spec = importlib.util.spec_from_file_location("task_scope_tool", TOOL_PATH)
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
    ).stdout


def _write(repo: Path, relative: str, content: str) -> Path:
    path = repo / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return path


def _task(
    task_id: str,
    status: str,
    owned_surface: str,
    assignee: str = "real-session-123",
    blocker: str | None = None,
    extra_sections: str = "",
) -> str:
    blocker_section = f"\n## Blocker\n\n- {blocker}\n" if blocker else ""
    return f"""# {task_id}

- Task ID: {task_id}
- Status: {status}
- Assignee: {assignee}

## Dependencies

- None

## Owned surface

- `{owned_surface}`
{blocker_section}{extra_sections}"""


def _validate_task(repo: Path, task_id: str):
    module = _tool()
    result = module.run_validation(task_id, repo, repo / "plan")
    return result["valid"], result


def _validate(repo: Path):
    return _validate_task(repo, "V1-FND-003")


@pytest.fixture()
def gated_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Test")
    _write(repo, "plan/v0/V0-DOM-001.md", _task("V0-DOM-001", "Done", "v0/**"))
    _write(
        repo,
        "plan/v1/V1-FND-003.md",
        _task("V1-FND-003", "Planned", "src/owned/**"),
    )
    _git(repo, "add", "plan")
    _git(repo, "commit", "-qm", "task baselines")
    return repo


def test_owned_surface_edit_cannot_expand_write_set(gated_repo: Path):
    task_path = gated_repo / "plan/v1/V1-FND-003.md"
    task_path.write_text(
        _task("V1-FND-003", "Planned", "src/owned/**") + "\n- `src/escape/**`\n",
        encoding="utf-8",
    )
    _write(gated_repo, "src/escape/file.py", "x = 1\n")

    valid, result = _validate(gated_repo)

    assert valid is False
    assert any(
        finding["reason"] == "task Markdown changed outside Status or Assignee metadata"
        for finding in result["findings"]
    )
    assert any(finding["path"] == "src/escape/file.py" for finding in result["findings"])


def test_only_metadata_and_owned_surface_are_valid(gated_repo: Path):
    task_path = gated_repo / "plan/v1/V1-FND-003.md"
    task_path.write_text(
        _task("V1-FND-003", "InProgress", "src/owned/**", "real-session-456"),
        encoding="utf-8",
    )
    _write(gated_repo, "src/owned/file.py", "x = 1\n")
    _write(gated_repo, "evidence/V1-FND-003/result.json", "{}\n")

    valid, result = _validate(gated_repo)

    assert valid is True
    assert result["findings"] == []
    assert result["metadata_errors"] == []


@pytest.mark.parametrize("status", ["Done", "Blocked", "NotApplicable"])
def test_closed_or_non_executable_task_cannot_write(gated_repo: Path, status: str):
    task_path = gated_repo / "plan/v1/V1-FND-003.md"
    task_path.write_text(
        _task("V1-FND-003", status, "src/owned/**"), encoding="utf-8"
    )
    _write(gated_repo, "src/owned/file.py", "x = 1\n")

    valid, result = _validate(gated_repo)

    assert valid is False
    if status == "Blocked":
        assert any(
            finding["reason"] == "Executable-to-Blocked transition cannot write non-task paths"
            for finding in result["findings"]
        )
    else:
        assert any(
            "expected 'Planned' or 'InProgress'" in error
            for error in result["metadata_errors"]
        )


def test_open_preceding_release_gate_blocks_write(gated_repo: Path):
    v0_task = gated_repo / "plan/v0/V0-DOM-001.md"
    v0_task.write_text(_task("V0-DOM-001", "Blocked", "v0/**"), encoding="utf-8")
    _git(gated_repo, "add", "plan/v0/V0-DOM-001.md")
    _git(gated_repo, "commit", "-qm", "open V0 gate")
    _write(gated_repo, "src/owned/file.py", "x = 1\n")

    valid, result = _validate(gated_repo)

    assert valid is False
    assert any("GATE-V0-EXIT is open" in error for error in result["metadata_errors"])


def test_untracked_task_cannot_supply_its_own_allowlist(tmp_path: Path):
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Test")
    _write(repo, ".gitignore", "")
    _git(repo, "add", ".gitignore")
    _git(repo, "commit", "-qm", "baseline")
    _write(repo, "plan/v1/V1-FND-003.md", _task("V1-FND-003", "Planned", "src/**"))
    _write(repo, "src/escape.py", "x = 1\n")

    valid, result = _validate(repo)

    assert valid is False
    assert any(
        finding["reason"] == "task Markdown has no committed baseline"
        for finding in result["findings"]
    )


def test_blocked_to_in_progress_can_remove_only_blocker(tmp_path: Path):
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Test")
    task_path = _write(
        repo,
        "plan/v0/V0-GOV-001.md",
        _task(
            "V0-GOV-001",
            "Blocked",
            "tools/task-scope/**",
            blocker="External evidence is missing; ancak it can be resumed after proof.",
        ),
    )
    _git(repo, "add", "plan")
    _git(repo, "commit", "-qm", "blocked baseline")

    task_path.write_text(
        _task("V0-GOV-001", "InProgress", "tools/task-scope/**"),
        encoding="utf-8",
    )

    valid, result = _validate_task(repo, "V0-GOV-001")

    assert valid is True
    assert result["findings"] == []


def test_in_progress_to_blocked_requires_unlockable_blocker(tmp_path: Path):
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Test")
    task_path = _write(
        repo,
        "plan/v0/V0-GOV-001.md",
        _task("V0-GOV-001", "InProgress", "tools/task-scope/**"),
    )
    _git(repo, "add", "plan")
    _git(repo, "commit", "-qm", "in progress baseline")

    task_path.write_text(
        _task(
            "V0-GOV-001",
            "Blocked",
            "tools/task-scope/**",
            blocker="External evidence is missing; ancak it can be resumed after proof.",
        ),
        encoding="utf-8",
    )

    valid, result = _validate_task(repo, "V0-GOV-001")

    assert valid is True
    assert result["findings"] == []


def test_blocker_transition_rejects_goal_change(tmp_path: Path):
    repo = tmp_path / "repo"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "test@example.com")
    _git(repo, "config", "user.name", "Test")
    task_path = _write(
        repo,
        "plan/v0/V0-GOV-001.md",
        _task(
            "V0-GOV-001",
            "Blocked",
            "tools/task-scope/**",
            blocker="External evidence is missing; ancak it can be resumed after proof.",
            extra_sections="\n## Goal\n\n- Preserve the original goal.\n",
        ),
    )
    _git(repo, "add", "plan")
    _git(repo, "commit", "-qm", "blocked baseline")

    task_path.write_text(
        _task(
            "V0-GOV-001",
            "InProgress",
            "tools/task-scope/**",
            extra_sections="\n## Goal\n\n- Change the original goal.\n",
        ),
        encoding="utf-8",
    )

    valid, result = _validate_task(repo, "V0-GOV-001")

    assert valid is False
    assert any(
        finding["reason"] == "task Markdown changed outside Status or Assignee metadata"
        for finding in result["findings"]
    )
