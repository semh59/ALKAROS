from __future__ import annotations

"""Tests for the task-scope tool diff mode and allowlist shape filter.

Covers: committed-diff detection (--diff-base), clean-worktree CI behaviour,
rename paths in diff mode, and Owned surface backtick fragments that are not
path-shaped (task IDs, prose) never entering the allowlist.
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


def _commit(repo: Path, rel: str, content: str = "x\n") -> Path:
    """Write, stage, and commit a file."""
    p = _write(repo, rel, content)
    _git(repo, "add", rel)
    _git(repo, "commit", "-q", "-m", f"add {rel}")
    return p


def run_validation(task_id: str, repo_root: Path, plan_dir: Path):
    mod = _load_module()
    result = mod.run_validation(task_id, repo_root, plan_dir)
    return (0 if result["valid"] else 1, result)


def run_diff_validation(
    task_id: str, repo_root: Path, plan_dir: Path, base: str
):
    mod = _load_module()
    result = mod.run_validation(task_id, repo_root, plan_dir, diff_base=base)
    return (0 if result["valid"] else 1, result)


# ---------------------------------------------------------------------------
# Diff mode
# ---------------------------------------------------------------------------

class TestDiffMode:
    def test_committed_out_of_scope_change_detected(
        self, write_task, make_repo, make_plan
    ):
        write_task()
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _commit(make_repo, "src/other_module/file.py")
        exit_code, result = run_diff_validation(
            "V1-FND-003", make_repo, make_plan, base
        )
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "src/other_module/file.py" in paths

    def test_committed_in_scope_change_accepted(
        self, write_task, make_repo, make_plan
    ):
        write_task()
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _commit(make_repo, "tools/task-scope/new_file.py")
        exit_code, result = run_diff_validation(
            "V1-FND-003", make_repo, make_plan, base
        )
        assert exit_code == 0
        assert result["findings"] == []

    def test_clean_worktree_with_committed_change_still_fails(
        self, write_task, make_repo, make_plan
    ):
        """A fresh checkout has a clean worktree; diff mode must not pass."""
        write_task()
        _git(make_repo, "add", "plan")
        _git(make_repo, "commit", "-q", "-m", "add plan")
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _commit(make_repo, "src/other_module/file.py")
        assert _git(make_repo, "status", "--porcelain").strip() == ""
        exit_code, result = run_diff_validation(
            "V1-FND-003", make_repo, make_plan, base
        )
        assert exit_code == 1

    def test_worktree_only_changes_not_in_diff_mode(
        self, write_task, make_repo, make_plan
    ):
        """Diff mode inspects commits only; dirty worktree files are ignored."""
        write_task()
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        _write(make_repo, "random/untracked.txt")
        exit_code, result = run_diff_validation(
            "V1-FND-003", make_repo, make_plan, base
        )
        assert exit_code == 0

    def test_rename_both_paths_checked_in_diff_mode(
        self, write_task, make_repo, make_plan
    ):
        write_task(owned_surface="- `tools/task-scope/**`")
        _commit(make_repo, "src/old_name.py", "content\n")
        base = _git(make_repo, "rev-parse", "HEAD").strip()
        (make_repo / "tools" / "task-scope").mkdir(parents=True, exist_ok=True)
        _git(make_repo, "mv", "src/old_name.py", "tools/task-scope/new_name.py")
        _git(make_repo, "commit", "-q", "-m", "rename")
        exit_code, result = run_diff_validation(
            "V1-FND-003", make_repo, make_plan, base
        )
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert any("src/old_name.py" in p for p in paths)


# ---------------------------------------------------------------------------
# Allowlist shape filter
# ---------------------------------------------------------------------------

class TestAllowlistShapeFilter:
    def test_task_id_fragment_not_an_allowlist_entry(
        self, write_task, make_repo, make_plan
    ):
        write_task(owned_surface="- `V1-FND-999`")
        _write(make_repo, "V1-FND-999")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = [f["path"] for f in result["findings"]]
        assert "v1-fnd-999" in paths

    def test_prose_fragment_not_an_allowlist_entry(
        self, write_task, make_repo, make_plan
    ):
        write_task(owned_surface="- `görev` içeriği `şu` durumdadır")
        _write(make_repo, "görev")
        _write(make_repo, "şu")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 1
        paths = {f["path"] for f in result["findings"]}
        assert "görev" in paths
        assert "şu" in paths

    def test_path_shaped_fragment_still_allowed(
        self, write_task, make_repo, make_plan
    ):
        write_task(owned_surface="- `tools/task-scope/**`")
        _write(make_repo, "tools/task-scope/a.py")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0

    def test_dotted_filename_fragment_allowed(
        self, write_task, make_repo, make_plan
    ):
        write_task(owned_surface="- `ALKAROS.slnx`")
        _write(make_repo, "ALKAROS.slnx")
        exit_code, result = run_validation("V1-FND-003", make_repo, make_plan)
        assert exit_code == 0
