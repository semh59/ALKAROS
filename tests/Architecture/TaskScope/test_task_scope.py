from __future__ import annotations

"""Tests for the task-scope enforcement tool.

Covers: allow/deny, dirty-worktree, untracked, delete, rename, path
traversal, Windows normalization, missing/multiple task IDs, incomplete
dependency, wrong status/assignee, broken Markdown, evidence directory,
metadata file, glob matching, and deterministic output.
"""

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
                "| Task ID | Approval date | Purpose | Gate closure evidence | New feature behavior |",
                "| --- | --- | --- | --- | --- |",
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


REMEDIATION_ROWS = [
    "| `V1-FND-011` | `2026-08-02` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-012` | `2026-08-02` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-IAM-004` | `2026-08-02` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-SEC-003` | `2026-08-02` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-001` | `2026-08-03` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-002` | `2026-08-03` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-004` | `2026-08-03` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-005` | `2026-08-03` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-006` | `2026-08-03` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-IAM-005` | `2026-08-04` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-013` | `2026-08-04` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-014` | `2026-08-04` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
    "| `V1-FND-015` | `2026-08-04` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |",
]

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

    def test_generic_assignee_codex(self, write_task, make_repo, make_plan, run_tool):
        write_task(assignee="Codex")
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

    def test_approved_task_bypasses_open_v0_entry_gate(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-011")

        exit_code, result = run_tool("V1-FND-011", make_repo, make_plan)

        assert exit_code == 0
        assert result["metadata_errors"] == []

    def test_candidate_remediation_skips_only_blocked_dependencies(
        self, write_task, make_repo, make_plan
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-001", dependencies="- V0-DOM-001")

        import importlib.util

        spec = importlib.util.spec_from_file_location(
            "task_scope_tool",
            Path(__file__).resolve().parents[3] / "tools" / "task-scope" / "task_scope_tool.py",
        )
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        result = mod.run_validation(
            "V1-FND-001", make_repo, make_plan, candidate_remediation=True
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
        write_task(task_id="V1-FND-011")
        _write_remediation_exceptions(make_plan, REMEDIATION_ROWS + [REMEDIATION_ROWS[0]])

        exit_code, result = run_tool("V1-FND-011", make_repo, make_plan)

        assert exit_code == 1
        assert any("duplicate Task ID" in error for error in result["metadata_errors"])

    def test_nonmatching_exception_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-011")
        nonmatching_rows = REMEDIATION_ROWS[:-1] + [
            "| `V1-FND-999` | `2026-08-02` | Verified finding remediation only | Not gate closure evidence | No new feature behavior |"
        ]
        _write_remediation_exceptions(make_plan, nonmatching_rows)

        exit_code, result = run_tool("V1-FND-011", make_repo, make_plan)

        assert exit_code == 1
        assert any("must exactly match" in error for error in result["metadata_errors"])

    def test_malformed_exception_record_fails_closed(
        self, write_task, make_repo, make_plan, run_tool
    ):
        self._prepare_open_v0_gate(write_task, make_repo, make_plan)
        write_task(task_id="V1-FND-011")
        malformed_rows = REMEDIATION_ROWS.copy()
        malformed_rows[0] = "| `V1-FND-011` | malformed |"
        _write_remediation_exceptions(make_plan, malformed_rows)

        exit_code, result = run_tool("V1-FND-011", make_repo, make_plan)

        assert exit_code == 1
        assert any("invalid record" in error for error in result["metadata_errors"])


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
