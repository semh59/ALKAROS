#!/usr/bin/env python3
"""
ALKAROS Build Provenance Verifier (V0-GOV-047).
Inspects production Release assemblies and validates that embedded repository commit
matches the candidate Git commit SHA.
"""

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Tuple

HEX40_PATTERN = re.compile(rb"[0-9a-f]{40}")
INFO_VER_PATTERN = re.compile(rb"\d+\.\d+\.\d+\+([0-9a-f]{40})")


def get_git_head_sha(repo_root: Path) -> str:
    """Return the 40-character commit SHA of HEAD."""
    res = subprocess.run(
        ["git", "-C", str(repo_root), "rev-parse", "HEAD"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
    )
    return res.stdout.strip().lower()


def find_production_projects(repo_root: Path) -> List[Path]:
    """Find all .csproj files under src/ (production projects)."""
    src_dir = repo_root / "src"
    if not src_dir.is_dir():
        return []
    return sorted(p for p in src_dir.rglob("*.csproj") if "bin" not in p.parts and "obj" not in p.parts)


def extract_embedded_commit(dll_path: Path) -> Optional[str]:
    """Extract embedded 40-character hex commit SHA from a .NET assembly."""
    if not dll_path.is_file():
        return None
    data = dll_path.read_bytes()
    # 1. Search for informational version with +<sha>
    info_match = INFO_VER_PATTERN.search(data)
    if info_match:
        return info_match.group(1).decode("ascii").lower()

    # 2. Search for any hex40
    hex_matches = HEX40_PATTERN.findall(data)
    if hex_matches:
        return hex_matches[0].decode("ascii").lower()

    return None


def verify_provenance(
    repo_root: Path,
    candidate_sha: Optional[str] = None
) -> Tuple[bool, Dict[str, object]]:
    """Verify that all production Release assemblies match the candidate SHA."""
    if candidate_sha is None:
        candidate_sha = get_git_head_sha(repo_root)
    else:
        candidate_sha = candidate_sha.strip().lower()

    if len(candidate_sha) != 40 or not re.match(r"^[0-9a-f]{40}$", candidate_sha):
        return False, {
            "error": f"Invalid candidate SHA format: {candidate_sha}",
            "candidate_sha": candidate_sha,
            "matched": [],
            "mismatched": [],
            "missing": [],
        }

    projects = find_production_projects(repo_root)
    matched = []
    mismatched = []
    missing = []

    for proj in projects:
        proj_name = proj.stem
        dll_path = proj.parent / "bin" / "Release" / "net8.0" / f"{proj_name}.dll"
        rel_path = dll_path.relative_to(repo_root).as_posix() if dll_path.exists() else proj.relative_to(repo_root).as_posix()

        if not dll_path.is_file():
            missing.append({
                "project": proj_name,
                "path": rel_path,
                "reason": "Release assembly DLL not found"
            })
            continue

        embedded_sha = extract_embedded_commit(dll_path)
        if embedded_sha is None:
            mismatched.append({
                "project": proj_name,
                "path": rel_path,
                "found_sha": None,
                "expected_sha": candidate_sha,
                "reason": "No embedded commit SHA found in assembly"
            })
        elif embedded_sha != candidate_sha:
            mismatched.append({
                "project": proj_name,
                "path": rel_path,
                "found_sha": embedded_sha,
                "expected_sha": candidate_sha,
                "reason": f"Commit SHA mismatch: found {embedded_sha}, expected {candidate_sha}"
            })
        else:
            matched.append({
                "project": proj_name,
                "path": rel_path,
                "sha": embedded_sha
            })

    is_valid = len(missing) == 0 and len(mismatched) == 0 and len(matched) > 0
    report = {
        "candidate_sha": candidate_sha,
        "total_projects": len(projects),
        "matched_count": len(matched),
        "missing_count": len(missing),
        "mismatched_count": len(mismatched),
        "matched": matched,
        "missing": missing,
        "mismatched": mismatched,
    }
    return is_valid, report


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Verify build provenance across production assemblies.")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--candidate-sha", type=str, default=None, help="Expected 40-char commit SHA")
    parser.add_argument("--json", action="store_true", help="Output JSON report")

    args = parser.parse_args(argv)
    is_valid, report = verify_provenance(args.repo_root, args.candidate_sha)

    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(f"Candidate SHA: {report.get('candidate_sha')}")
        print(f"Projects checked: {report.get('total_projects')}")
        print(f"Matched: {report.get('matched_count')}")
        print(f"Missing: {report.get('missing_count')}")
        print(f"Mismatched: {report.get('mismatched_count')}")
        if not is_valid:
            print("\nErrors:")
            for m in report.get("missing", []):
                print(f"  [MISSING] {m['project']}: {m['reason']}")
            for mm in report.get("mismatched", []):
                print(f"  [MISMATCH] {mm['project']}: {mm['reason']}")

    return 0 if is_valid else 1


if __name__ == "__main__":
    sys.exit(main())
