from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

import pytest


REPOSITORY = Path(__file__).resolve().parents[3]
CONTRACT_MARKER = "<!-- PLAN_AUDIT_REMEDIATION_ADMISSION:END -->"
GATES_MARKER = "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->"
FND016_ROW = (
    "| `V1-FND-016` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | "
    "Not gate closure evidence | No new feature behavior |"
)
FND017_ROW = FND016_ROW.replace("V1-FND-016", "V1-FND-017")
FND023_ROW = (
    "| `V1-FND-023` | `2026-08-11` | `CORR:C52;CORR:C53;CORR:C54` | "
    "Verified finding remediation only | Not gate closure evidence | No new feature behavior |"
)


def _copy_validation_workspace(tmp_path: Path) -> Path:
    workspace = tmp_path / "workspace"
    shutil.copytree(REPOSITORY / "plan", workspace / "plan")
    shutil.copy2(REPOSITORY / "AGENTS.md", workspace / "AGENTS.md")
    shutil.copytree(
        REPOSITORY / "tools" / "plan-audit", workspace / "tools" / "plan-audit"
    )
    shutil.copytree(
        REPOSITORY / "tools" / "task-scope", workspace / "tools" / "task-scope"
    )
    subprocess.run(["git", "init", "-q"], cwd=workspace, check=True)
    subprocess.run(["git", "add", "."], cwd=workspace, check=True)
    return workspace


def _run_validate(workspace: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-B", "tools/plan-audit/plan_audit_tool.py", "validate"],
        cwd=workspace,
        check=False,
        capture_output=True,
        encoding="utf-8",
    )


def _replace(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    assert old in text
    path.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")


def _replace_contract(workspace: Path, old: str, new: str) -> None:
    _replace(workspace / "plan" / "VALIDATION_CONTRACT.md", old, new)


class TestRemediationAdmissionSemanticValidation:
    def test_exact_19_record_tuple_is_semantically_valid(self, tmp_path: Path) -> None:
        result = _run_validate(_copy_validation_workspace(tmp_path))

        assert result.returncode == 0, result.stdout + result.stderr
        assert "Validation errors: 0" in result.stdout

    @pytest.mark.parametrize(
        ("mutation", "expected_errors"),
        [
            (
                lambda workspace: _replace_contract(
                    workspace,
                    FND023_ROW,
                    FND023_ROW.replace("CORR:C52;CORR:C53;CORR:C54", "CORR:C52"),
                ),
                ("SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_SOURCE V1-FND-023",),
            ),
            (
                lambda workspace: _replace_contract(
                    workspace,
                    FND023_ROW,
                    FND023_ROW.replace("2026-08-11", "2026-08-10"),
                ),
                ("SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_DATE V1-FND-023",),
            ),
            (
                lambda workspace: _replace_contract(workspace, FND017_ROW, FND016_ROW),
                ("SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_DUPLICATE V1-FND-016",),
            ),
            (
                lambda workspace: _replace_contract(
                    workspace,
                    FND016_ROW + "\n" + FND017_ROW,
                    FND017_ROW + "\n" + FND016_ROW,
                ),
                ("SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_ORDER",),
            ),
            (
                lambda workspace: _replace_contract(workspace, FND017_ROW + "\n", ""),
                (
                    "SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_COUNT expected=19 actual=18",
                    "SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_MISSING V1-FND-017",
                ),
            ),
            (
                lambda workspace: _replace_contract(
                    workspace,
                    CONTRACT_MARKER,
                    "| `V1-FND-024` | `2026-08-10` | `CORR:C52` | Verified finding remediation only | "
                    "Not gate closure evidence | No new feature behavior |\n" + CONTRACT_MARKER,
                ),
                (
                    "SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_COUNT expected=19 actual=20",
                    "SEMANTIC_REMEDIATION_ADMISSION_CONTRACT_EXTRA V1-FND-024",
                ),
            ),
        ],
        ids=["source", "date", "duplicate", "order", "missing", "extra"],
    )
    def test_byte_valid_contract_divergence_fails_closed(
        self, tmp_path: Path, mutation, expected_errors: tuple[str, ...]
    ) -> None:
        workspace = _copy_validation_workspace(tmp_path)
        mutation(workspace)

        result = _run_validate(workspace)

        assert result.returncode == 1
        for expected_error in expected_errors:
            assert expected_error in result.stdout

    def test_gate_tuple_source_divergence_fails_closed(self, tmp_path: Path) -> None:
        workspace = _copy_validation_workspace(tmp_path)
        _replace(
            workspace / "plan" / "GATES.md",
            FND023_ROW,
            FND023_ROW.replace("CORR:C52;CORR:C53;CORR:C54", "CORR:C52"),
        )

        result = _run_validate(workspace)

        assert result.returncode == 1
        assert "SEMANTIC_REMEDIATION_ADMISSION_GATES_SOURCE V1-FND-023" in result.stdout

    def test_task_scope_tuple_source_divergence_fails_closed(self, tmp_path: Path) -> None:
        workspace = _copy_validation_workspace(tmp_path)
        _replace(
            workspace / "tools" / "task-scope" / "task_scope_tool.py",
            '"V1-FND-023": ("2026-08-11", "CORR:C52;CORR:C53;CORR:C54"),',
            '"V1-FND-023": ("2026-08-11", "CORR:C52"),',
        )

        result = _run_validate(workspace)

        assert result.returncode == 1
        assert "SEMANTIC_REMEDIATION_ADMISSION_TASK_SCOPE_SOURCE V1-FND-023" in result.stdout
