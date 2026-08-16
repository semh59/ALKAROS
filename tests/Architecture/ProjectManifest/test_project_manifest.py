"""
Unit and integration tests for Project Manifest consistency (V0-GOV-040).
"""

import importlib.util
from pathlib import Path
import pytest


def _load_tool():
    tool_path = Path(__file__).resolve().parents[3] / "tools" / "project-manifest" / "project_manifest_tool.py"
    spec = importlib.util.spec_from_file_location("project_manifest_tool", tool_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


tool = _load_tool()


def test_current_repository_project_manifest_is_valid():
    """Verify that current repository solution, disk, and references match 100%."""
    repo_root = tool.find_repo_root()
    is_valid, errors = tool.validate_project_manifest(repo_root)

    assert is_valid, f"Project manifest drift detected: {errors}"
    assert len(errors["slnx_missing_on_disk"]) == 0
    assert len(errors["disk_missing_in_slnx"]) == 0
    assert len(errors["broken_project_references"]) == 0


def test_detects_disk_project_missing_in_slnx(tmp_path: Path):
    """Negative test: detects a .csproj that exists on disk but is omitted from slnx."""
    slnx_content = """
    <Solution>
      <Project Path="src/ModA/ModA.csproj" />
    </Solution>
    """
    (tmp_path / "ALKAROS.slnx").write_text(slnx_content, encoding="utf-8")
    (tmp_path / "src" / "ModA").mkdir(parents=True)
    (tmp_path / "src" / "ModA" / "ModA.csproj").write_text("<Project></Project>", encoding="utf-8")

    # Extra unlisted project
    (tmp_path / "src" / "ModB").mkdir(parents=True)
    (tmp_path / "src" / "ModB" / "ModB.csproj").write_text("<Project></Project>", encoding="utf-8")

    is_valid, errors = tool.validate_project_manifest(tmp_path)
    assert not is_valid
    assert any("ModB.csproj" in item for item in errors["disk_missing_in_slnx"])


def test_detects_slnx_project_missing_on_disk(tmp_path: Path):
    """Negative test: detects a project declared in slnx that does not exist on disk."""
    slnx_content = """
    <Solution>
      <Project Path="src/ModA/ModA.csproj" />
      <Project Path="src/NonExistent/NonExistent.csproj" />
    </Solution>
    """
    (tmp_path / "ALKAROS.slnx").write_text(slnx_content, encoding="utf-8")
    (tmp_path / "src" / "ModA").mkdir(parents=True)
    (tmp_path / "src" / "ModA" / "ModA.csproj").write_text("<Project></Project>", encoding="utf-8")

    is_valid, errors = tool.validate_project_manifest(tmp_path)
    assert not is_valid
    assert any("NonExistent.csproj" in item for item in errors["slnx_missing_on_disk"])


def test_detects_broken_project_reference(tmp_path: Path):
    """Negative test: detects broken ProjectReference."""
    slnx_content = """
    <Solution>
      <Project Path="src/ModA/ModA.csproj" />
    </Solution>
    """
    (tmp_path / "ALKAROS.slnx").write_text(slnx_content, encoding="utf-8")
    (tmp_path / "src" / "ModA").mkdir(parents=True)
    csproj_with_broken_ref = """
    <Project Sdk="Microsoft.NET.Sdk">
      <ItemGroup>
        <ProjectReference Include="../Missing/Missing.csproj" />
      </ItemGroup>
    </Project>
    """
    (tmp_path / "src" / "ModA" / "ModA.csproj").write_text(csproj_with_broken_ref, encoding="utf-8")

    is_valid, errors = tool.validate_project_manifest(tmp_path)
    assert not is_valid
    assert len(errors["broken_project_references"]) > 0
