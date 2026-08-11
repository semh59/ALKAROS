from __future__ import annotations

import subprocess
from pathlib import Path


REPOSITORY = Path(__file__).resolve().parents[3]
TEST_PROJECT = REPOSITORY / "tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj"
HELPER_PROJECT = REPOSITORY / "tests/BuildingBlocks/TestHelpers/ALKAROS.TestHelpers.csproj"


def _evaluated_property(project: Path, property_name: str) -> str:
    result = subprocess.run(
        ["dotnet", "msbuild", str(project), f"-getProperty:{property_name}", "-nologo"],
        cwd=REPOSITORY,
        capture_output=True,
        text=True,
        check=True,
    )
    lines = result.stdout.splitlines()
    assert len(lines) == 1, result.stdout
    return lines[0].strip()


def test_alkaros_test_project_evaluates_as_vstest_project() -> None:
    assert _evaluated_property(TEST_PROJECT, "IsTestProject") == "true"


def test_test_helper_remains_non_test_project() -> None:
    assert _evaluated_property(HELPER_PROJECT, "IsTestProject") == "false"
