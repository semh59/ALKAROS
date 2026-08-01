from __future__ import annotations

"""Tests for Owned surface continuation-line parsing.

Covers: backtick paths wrapped onto continuation lines after a ``- `` bullet
enter the allowlist; continuation fragments after prose or after the
"- Bu görev..." reset line never enter the allowlist; path-shape filtering
still applies to continuation fragments; case-insensitive matching keeps
working for continuation paths.
"""

import importlib.util
import subprocess
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parents[3] / "tools" / "task-scope"


def _load_module():
    spec = importlib.util.spec_from_file_location(
        "task_scope_tool",
        TOOLS_DIR / "task_scope_tool.py",
    )
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


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


def run_validation(task_id: str, repo_root: Path, plan_dir: Path):
    mod = _load_module()
    result = mod.run_validation(task_id, repo_root, plan_dir)
    return (0 if result["valid"] else 1, result)


class TestContinuationLineParsing:
    def test_wrapped_continuation_paths_allowed(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `tools/task-scope/core.py`,\n"
            "  `tools/task-scope/wrapped.py`,\n"
            "  `tools/task-scope/deep/other.py`"
        )
        _write(make_repo, "tools/task-scope/wrapped.py")
        _write(make_repo, "tools/task-scope/deep/other.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
        assert result["findings"] == []

    def test_continuation_after_prose_line_ignored(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `tools/task-scope/core.py`\n"
            "Some prose without a bullet, then\n"
            "  `tools/task-scope/not-owned.py`"
        )
        _write(make_repo, "tools/task-scope/not-owned.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "tools/task-scope/not-owned.py" in paths

    def test_continuation_after_reset_line_ignored(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `tools/task-scope/core.py`\n"
            "- Bu görev başka bir task'ın owned surface alanını değiştiremez;\n"
            "  `tools/task-scope/not-owned.py`"
        )
        _write(make_repo, "tools/task-scope/not-owned.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "tools/task-scope/not-owned.py" in paths

    def test_orphan_continuation_without_bullet_ignored(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `tools/task-scope/core.py`\n"
            "\n"
            "  `tools/task-scope/not-owned.py`"
        )
        _write(make_repo, "tools/task-scope/not-owned.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "tools/task-scope/not-owned.py" in paths

    def test_continuation_fragments_path_shape_filtered(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `V1-FND-999` aracı,\n"
            "  `görev` içeriği\n"
            "  `tools/task-scope/real.py`"
        )
        _write(make_repo, "tools/task-scope/real.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
        _write(make_repo, "görev")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "görev" in paths

    def test_continuation_path_case_insensitive(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `src/Host/Composition/Migrations/MigrationExecutor.cs`,\n"
            "  `src/Host/Composition/Migrations/PsqlScriptRunner.cs`"
        )
        _write(make_repo, "src/host/composition/migrations/psqlscriptrunner.cs")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_new_bullet_after_continuation_parses_normally(
        self, write_task, make_repo, make_plan
    ):
        write_task(
            owned_surface="- `tools/task-scope/core.py`,\n"
            "  `tools/task-scope/wrapped.py`\n"
            "- `tests/architecture/taskscope/new_test.py`"
        )
        _write(make_repo, "tests/architecture/taskscope/new_test.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
