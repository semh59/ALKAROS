from __future__ import annotations

"""Shared pytest fixtures for task-scope enforcement tests."""

import subprocess
from pathlib import Path

import pytest

TOOLS_DIR = Path(__file__).resolve().parents[3] / "tools" / "task-scope"


def _run_git(repo: Path, *args: str) -> str:
    """Run a git command inside *repo* and return stdout."""
    result = subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True,
        text=True,
        check=True,
    )
    return result.stdout


def _init_repo(repo: Path) -> None:
    """Initialise a git repo with a baseline commit."""
    _run_git(repo, "init", "-q")
    _run_git(repo, "config", "user.email", "test@example.com")
    _run_git(repo, "config", "user.name", "Test")
    _run_git(repo, "config", "core.autocrlf", "false")
    (repo / ".gitignore").write_text("*.pyc\n", encoding="utf-8")
    _run_git(repo, "add", ".gitignore")
    _run_git(repo, "commit", "-q", "-m", "init")


VALID_TASK_TEMPLATE = """\
# {task_id} - Test task

- Task ID: {task_id}
- Status: {status}
- Assignee: {assignee}
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10

## Goal

Test goal.

## Owned surface

{owned_surface}

## In scope

- Test scope.

## Out of scope

- Nothing.

## Dependencies

{dependencies}

## Deliverables

- Test deliverable.

## Acceptance evidence

- Test evidence.

## Handoff

- None
"""

DONE_TASK_TEMPLATE = """\
# {task_id} - Done dependency

- Task ID: {task_id}
- Status: Done
- Assignee: test-session
- Work type: implementation
- Surface state: Done

## Source basis

- PDF:I.7-I.10

## Goal

Done dependency.

## Owned surface

- `test/**`

## In scope

- Test.

## Out of scope

- Nothing.

## Dependencies

- None

## Deliverables

- Test.

## Acceptance evidence

- Test.

## Handoff

- None
"""


def _write_task_file(
    plan_dir: Path,
    task_id: str,
    status: str = "InProgress",
    assignee: str = "test-session",
    owned_surface: str = "- `tools/task-scope/**`\n- `tests/architecture/taskscope/**`",
    dependencies: str = "- None",
) -> Path:
    """Write a task Markdown file and return its path."""
    task_file = plan_dir / f"{task_id}.md"
    task_file.parent.mkdir(parents=True, exist_ok=True)
    task_file.write_text(
        VALID_TASK_TEMPLATE.format(
            task_id=task_id,
            status=status,
            assignee=assignee,
            owned_surface=owned_surface,
            dependencies=dependencies,
        ),
        encoding="utf-8",
    )
    return task_file


def _write_done_task(plan_dir: Path, task_id: str) -> Path:
    """Write a Done dependency task file."""
    task_file = plan_dir / f"{task_id}.md"
    task_file.parent.mkdir(parents=True, exist_ok=True)
    task_file.write_text(
        DONE_TASK_TEMPLATE.format(task_id=task_id),
        encoding="utf-8",
    )
    return task_file


def _commit_task_baseline(repo: Path, task_file: Path) -> None:
    """Commit a V0 prerequisite and immutable baseline for the active task."""
    prerequisite = repo / "plan" / "V0-DOM-001.md"
    if not prerequisite.exists():
        _write_done_task(prerequisite.parent, "V0-DOM-001")
    _run_git(repo, "add", "plan")
    _run_git(repo, "commit", "-q", "-m", "add task baseline")

    text = task_file.read_text(encoding="utf-8")
    task_file.write_text(
        text.replace("- Assignee: test-session", "- Assignee: test-session-active"),
        encoding="utf-8",
    )


@pytest.fixture()
def make_repo(tmp_path: Path) -> Path:
    """Create a temporary git repository and return its path."""
    repo = tmp_path / "repo"
    repo.mkdir()
    _init_repo(repo)
    return repo


@pytest.fixture()
def make_plan(make_repo: Path) -> Path:
    """Create a plan directory inside the repo and return its path."""
    plan = make_repo / "plan"
    plan.mkdir(exist_ok=True)
    return plan


@pytest.fixture()
def write_task(make_repo: Path, make_plan: Path):
    """Fixture factory to write task files."""
    def _write(
        task_id: str = "V1-FND-003",
        status: str = "InProgress",
        assignee: str = "test-session",
        owned_surface: str = "- `tools/task-scope/**`\n- `tests/architecture/taskscope/**`",
        dependencies: str = "- None",
    ) -> Path:
        task_file = _write_task_file(
            make_plan,
            task_id,
            status,
            assignee,
            owned_surface,
            dependencies,
        )
        _commit_task_baseline(make_repo, task_file)
        return task_file
    return _write


@pytest.fixture()
def write_done_dep(make_plan: Path):
    """Fixture factory to write Done dependency task files."""
    def _write(task_id: str) -> Path:
        return _write_done_task(make_plan, task_id)
    return _write


@pytest.fixture()
def run_tool():
    """Run the task-scope tool against a repo and return (exit_code, result_dict)."""
    import importlib.util

    spec = importlib.util.spec_from_file_location(
        "task_scope_tool",
        TOOLS_DIR / "task_scope_tool.py",
    )
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)

    def _run(
        task_id: str,
        repo_root: Path,
        plan_dir: Path,
    ):
        result = mod.run_validation(task_id, repo_root, plan_dir)
        return (0 if result["valid"] else 1, result)

    return _run
