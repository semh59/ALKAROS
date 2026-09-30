from __future__ import annotations

"""GATE-V14-EXIT admits exactly the v1.5 tasks listed in the approved GATES.md table."""

import subprocess
from pathlib import Path

_REASON = "KVKK saklama yurutmesi dis bagimlilik gerektirmez; v1.4 kapanisini beklemez"
_ROW = f"| `V15-KVK-001` | `2026-09-30` | {_REASON} |"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args], capture_output=True, text=True, check=True
    ).stdout


def _write_admission(plan_dir: Path, rows: list[str]) -> None:
    table = "\n".join(rows)
    (plan_dir / "GATES.md").write_text(
        "\n".join(
            [
                "# Version Gates",
                "",
                "<!-- V14_EXIT_AHEAD_ADMISSION:START -->",
                "| Task ID | Approval date | Reason |",
                "| --- | --- | --- |",
                table,
                "<!-- V14_EXIT_AHEAD_ADMISSION:END -->",
                "",
            ]
        ),
        encoding="utf-8",
    )


def _open_v14_gate(write_task, make_repo: Path) -> None:
    write_task(task_id="V14-QNB-001", status="Planned")
    _git(make_repo, "checkout", "--", "plan")


def _is_open_for(result: dict, task_id: str) -> bool:
    return any("GATE-V14-EXIT is open" in error and "V14-QNB-001 (Planned)" in error for error in result["metadata_errors"])


class TestV14ExitAheadAdmission:
    def test_the_admitted_v15_task_passes_while_the_gate_is_open(self, write_task, make_repo, make_plan, run_tool):
        _write_admission(make_plan, [_ROW])
        _open_v14_gate(write_task, make_repo)
        write_task(task_id="V15-KVK-001")

        exit_code, result = run_tool("V15-KVK-001", make_repo, make_plan)

        assert exit_code == 0
        assert result["metadata_errors"] == []

    def test_any_other_v15_task_keeps_the_gate_open(self, write_task, make_repo, make_plan, run_tool):
        _write_admission(make_plan, [_ROW])
        _open_v14_gate(write_task, make_repo)
        write_task(task_id="V15-KVK-002")

        exit_code, result = run_tool("V15-KVK-002", make_repo, make_plan)

        assert exit_code == 1
        assert _is_open_for(result, "V15-KVK-002")

    def test_a_missing_table_keeps_the_gate_open(self, write_task, make_repo, make_plan, run_tool):
        _open_v14_gate(write_task, make_repo)
        write_task(task_id="V15-KVK-001")

        exit_code, result = run_tool("V15-KVK-001", make_repo, make_plan)

        assert exit_code == 1
        assert _is_open_for(result, "V15-KVK-001")

    def test_a_table_that_differs_from_the_approval_fails_closed(self, write_task, make_repo, make_plan, run_tool):
        extra = "| `V15-KVK-002` | `2026-09-30` | baska gorev |"
        altered = f"| `V15-KVK-001` | `2026-10-01` | {_REASON} |"
        for rows in ([_ROW, extra], [altered], [_ROW, _ROW], ["| `V15-KVK-001` | broken |"]):
            _write_admission(make_plan, rows)
            _open_v14_gate(write_task, make_repo)
            write_task(task_id="V15-KVK-001")

            exit_code, result = run_tool("V15-KVK-001", make_repo, make_plan)

            assert exit_code == 1
            assert any("admission table rejected" in error for error in result["metadata_errors"])

    def test_the_admission_does_not_open_other_gates(self, write_task, make_repo, make_plan, run_tool):
        _write_admission(make_plan, [_ROW])
        write_task(task_id="V1-FND-003", status="Planned")
        _git(make_repo, "checkout", "--", "plan")
        write_task(task_id="V11-ALT-001")

        exit_code, result = run_tool("V11-ALT-001", make_repo, make_plan)

        assert exit_code == 1
        assert any("GATE-V1-EXIT is open" in error for error in result["metadata_errors"])
