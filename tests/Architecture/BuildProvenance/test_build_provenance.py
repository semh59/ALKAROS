"""
Unit and integration tests for Build Provenance Verification (V0-GOV-047).
"""

import importlib.util
from pathlib import Path
import pytest


def _load_tool():
    tool_path = Path(__file__).resolve().parents[3] / "tools" / "build-provenance" / "verify_build_provenance.py"
    spec = importlib.util.spec_from_file_location("verify_build_provenance", tool_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


tool = _load_tool()


def test_extract_embedded_commit_from_bytes(tmp_path: Path):
    """Verify that commit SHA is extracted properly from dummy DLL with informational version."""
    sha = "a9ebe9a000000000000000000000000000000000"
    dll = tmp_path / "Dummy.dll"
    content = b"MZ\x90\x00AssemblyInformationalVersionAttribute\x001.0.0+" + sha.encode("ascii") + b"\x00"
    dll.write_bytes(content)

    extracted = tool.extract_embedded_commit(dll)
    assert extracted == sha


def test_detects_stale_sha(tmp_path: Path):
    """Negative test: detects assembly with stale/mismatched SHA."""
    src_dir = tmp_path / "src" / "ModA"
    src_dir.mkdir(parents=True)
    (src_dir / "ModA.csproj").write_text("<Project></Project>", encoding="utf-8")

    bin_dir = src_dir / "bin" / "Release" / "net8.0"
    bin_dir.mkdir(parents=True)
    stale_sha = "1111111111111111111111111111111111111111"
    (bin_dir / "ModA.dll").write_bytes(b"1.0.0+" + stale_sha.encode("ascii"))

    expected_sha = "2222222222222222222222222222222222222222"
    is_valid, report = tool.verify_provenance(tmp_path, candidate_sha=expected_sha)

    assert not is_valid
    assert report["mismatched_count"] == 1
    assert report["mismatched"][0]["found_sha"] == stale_sha


def test_detects_missing_assembly(tmp_path: Path):
    """Negative test: detects missing Release DLL for a declared project."""
    src_dir = tmp_path / "src" / "ModA"
    src_dir.mkdir(parents=True)
    (src_dir / "ModA.csproj").write_text("<Project></Project>", encoding="utf-8")

    expected_sha = "2222222222222222222222222222222222222222"
    is_valid, report = tool.verify_provenance(tmp_path, candidate_sha=expected_sha)

    assert not is_valid
    assert report["missing_count"] == 1
    assert report["missing"][0]["project"] == "ModA"


def test_rejects_invalid_sha_format(tmp_path: Path):
    """Negative test: rejects non-40-hex SHA."""
    is_valid, report = tool.verify_provenance(tmp_path, candidate_sha="invalid-sha-123")
    assert not is_valid
    assert "error" in report
