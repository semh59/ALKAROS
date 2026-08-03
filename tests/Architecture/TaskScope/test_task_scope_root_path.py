from __future__ import annotations

"""Regression tests for repository-root path normalization."""

from pathlib import Path


def test_relative_and_absolute_roots_produce_the_same_result(
    write_task, make_plan: Path, make_repo: Path, monkeypatch, run_tool
) -> None:
    write_task()

    absolute_exit_code, absolute_result = run_tool(
        "V1-FND-003", make_repo, make_plan
    )

    monkeypatch.chdir(make_repo)
    relative_exit_code, relative_result = run_tool(
        "V1-FND-003", Path("."), Path("plan")
    )

    assert absolute_exit_code == relative_exit_code == 0
    assert absolute_result == relative_result
