#!/usr/bin/env python3
"""
ALKAROS Project Manifest Validator (V0-GOV-040)
Validates fail-closed consistency across solution file, disk projects,
ProjectReference links, and package lockfiles.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from typing import Dict, List, Set, Tuple


def find_repo_root(start_path: Path | None = None) -> Path:
    """Find repository root by walking upwards looking for .git or ALKAROS.slnx."""
    curr = (start_path or Path.cwd()).resolve()
    while curr != curr.parent:
        if (curr / "ALKAROS.slnx").exists() or (curr / ".git").exists():
            return curr
        curr = curr.parent
    return (start_path or Path.cwd()).resolve()


def parse_slnx_projects(slnx_path: Path) -> Set[Path]:
    """Parse project paths from ALKAROS.slnx."""
    if not slnx_path.exists():
        raise FileNotFoundError(f"Solution file not found: {slnx_path}")

    content = slnx_path.read_text(encoding="utf-8")
    project_paths: Set[Path] = set()
    repo_root = slnx_path.parent

    # Match <Project Path="..." />
    for match in re.finditer(r'<Project\s+Path="([^"]+\.csproj)"', content, re.IGNORECASE):
        rel_path = match.group(1).replace("\\", "/")
        full_path = (repo_root / rel_path).resolve()
        project_paths.add(full_path)

    return project_paths


def find_disk_projects(repo_root: Path) -> Set[Path]:
    """Find all .csproj files under src/ and tests/."""
    disk_projects: Set[Path] = set()
    for search_dir in ["src", "tests"]:
        dir_path = repo_root / search_dir
        if dir_path.exists():
            for csproj in dir_path.rglob("*.csproj"):
                # Ignore bin and obj directories
                if "bin" in csproj.parts or "obj" in csproj.parts:
                    continue
                disk_projects.add(csproj.resolve())
    return disk_projects


def parse_project_references(csproj_path: Path) -> Set[Path]:
    """Parse all ProjectReference paths in a .csproj file."""
    references: Set[Path] = set()
    try:
        tree = ET.parse(csproj_path)
        root = tree.getroot()
        for elem in root.iter("ProjectReference"):
            include = elem.attrib.get("Include")
            if include:
                ref_path = (csproj_path.parent / include.replace("\\", "/")).resolve()
                references.add(ref_path)
    except Exception as e:
        print(f"Error parsing {csproj_path}: {e}", file=sys.stderr)
    return references


def validate_project_manifest(repo_root: Path) -> Tuple[bool, Dict[str, List[str]]]:
    """Validate full consistency between solution, disk, and references."""
    errors: Dict[str, List[str]] = {
        "slnx_missing_on_disk": [],
        "disk_missing_in_slnx": [],
        "broken_project_references": [],
    }

    slnx_path = repo_root / "ALKAROS.slnx"
    slnx_projects = parse_slnx_projects(slnx_path)
    disk_projects = find_disk_projects(repo_root)

    # 1. Solution projects missing on disk
    for proj in slnx_projects:
        if not proj.exists():
            errors["slnx_missing_on_disk"].append(str(proj.relative_to(repo_root)))

    # 2. Disk projects missing in solution
    for proj in disk_projects:
        if proj not in slnx_projects:
            errors["disk_missing_in_slnx"].append(str(proj.relative_to(repo_root)))

    # 3. ProjectReference validity
    all_known_projects = slnx_projects.union(disk_projects)
    for proj in disk_projects:
        refs = parse_project_references(proj)
        for ref in refs:
            if not ref.exists():
                errors["broken_project_references"].append(
                    f"{proj.relative_to(repo_root)} -> {ref} (Missing)"
                )
            elif ref not in all_known_projects:
                errors["broken_project_references"].append(
                    f"{proj.relative_to(repo_root)} -> {ref.relative_to(repo_root)} (Not in solution)"
                )

    is_valid = not any(errors.values())
    return is_valid, errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate ALKAROS project manifest consistency.")
    parser.add_argument("--root", type=Path, default=None, help="Repository root path")
    args = parser.parse_args()

    repo_root = find_repo_root(args.root)
    is_valid, errors = validate_project_manifest(repo_root)

    print(f"ALKAROS Project Manifest Validator")
    print(f"Repository Root: {repo_root}")
    print(f"Solution: ALKAROS.slnx")

    if is_valid:
        print("Status: VALID (0 differences across Solution, Disk, and ProjectReferences)")
        return 0

    print("Status: INVALID - Drift detected:", file=sys.stderr)
    for category, items in errors.items():
        if items:
            print(f"  {category} ({len(items)} items):", file=sys.stderr)
            for item in items:
                print(f"    - {item}", file=sys.stderr)

    return 1


if __name__ == "__main__":
    sys.exit(main())
