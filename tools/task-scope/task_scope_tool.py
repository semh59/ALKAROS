from __future__ import annotations

"""Task scope enforcement tool for ALKAROS.

Parses a task Markdown file, builds a write allowlist from the task's
``Owned surface`` section, inspects either the current Git worktree or the
committed diff between a base ref and HEAD, and validates every changed path
against the allowlist.

Local preflight uses worktree mode (the default): staged, unstaged, untracked,
deleted and renamed paths are collected from ``git status --porcelain=v1``.
CI uses diff mode: ``--diff-base <ref>`` collects committed changes from
``git diff --name-status <ref>...HEAD`` so a fresh checkout still detects
out-of-scope paths.

Exit code 0 means every changed path is within scope.
Exit code 1 means one or more paths are out of scope or the task metadata
is invalid.

The output is machine-readable JSON on stdout so that both local preflight
and CI can consume the same result contract.
"""

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

WORKSPACE = Path(__file__).resolve().parents[2]
PLAN_DIR = WORKSPACE / "plan"

# ---------------------------------------------------------------------------
# Markdown task-file parser
# ---------------------------------------------------------------------------

_TASK_ID_PATTERN = re.compile(r"^- Task ID:\s*(.+?)\s*$", re.MULTILINE)
_STATUS_PATTERN = re.compile(r"^- Status:\s*(.+?)\s*$", re.MULTILINE)
_ASSIGNEE_PATTERN = re.compile(r"^- Assignee:\s*(.+?)\s*$", re.MULTILINE)
_DEPENDENCIES_HEADER = re.compile(r"^##\s+Dependencies\s*$", re.MULTILINE)
_OWNED_SURFACE_HEADER = re.compile(r"^##\s+Owned surface\s*$", re.MULTILINE)
_NEXT_HEADER = re.compile(r"^##\s+", re.MULTILINE)
_TASK_ID_FORMAT = re.compile(r"^V\d+-[A-Z]+-\d+$")
_BACKTICK_PATH = re.compile(r"`([^`]+)`")
_PATH_SHAPE = re.compile(r"[/\\.*?]")

VALID_STATUSES: Set[str] = {"Planned", "InProgress", "Done", "Blocked"}


class TaskParseError(Exception):
    """Raised when a task Markdown file cannot be parsed."""


class TaskMetadata:
    """Parsed metadata from a task Markdown file."""

    def __init__(
        self,
        task_id: str,
        status: str,
        assignee: str,
        dependencies: List[str],
        owned_surface: List[str],
        file_path: Path,
    ) -> None:
        self.task_id = task_id
        self.status = status
        self.assignee = assignee
        self.dependencies = dependencies
        self.owned_surface = owned_surface
        self.file_path = file_path

    def __repr__(self) -> str:
        return (
            f"TaskMetadata(task_id={self.task_id!r}, status={self.status!r}, "
            f"assignee={self.assignee!r}, dependencies={self.dependencies!r}, "
            f"owned_surface={self.owned_surface!r})"
        )


def _extract_section(text: str, header_pattern: re.Pattern) -> str:
    """Extract the text between a header and the next header of the same level."""
    match = header_pattern.search(text)
    if match is None:
        return ""
    start = match.end()
    next_header = _NEXT_HEADER.search(text, start)
    if next_header is None:
        return text[start:]
    return text[start:next_header.start()]


def parse_task_file(file_path: Path) -> TaskMetadata:
    """Parse a task Markdown file and return its metadata.

    Raises TaskParseError if the file is missing, unreadable, or does not
    contain exactly one Task ID.
    """
    if not file_path.is_file():
        raise TaskParseError(f"Task file not found: {file_path}")

    text = file_path.read_text(encoding="utf-8")

    task_id_matches = _TASK_ID_PATTERN.findall(text)
    if len(task_id_matches) != 1:
        raise TaskParseError(
            f"Expected exactly one Task ID, found {len(task_id_matches)} "
            f"in {file_path}"
        )
    task_id = task_id_matches[0].strip()
    if not _TASK_ID_FORMAT.match(task_id):
        raise TaskParseError(f"Invalid Task ID format: {task_id!r}")

    status_match = _STATUS_PATTERN.search(text)
    if status_match is None:
        raise TaskParseError(f"Status not found in {file_path}")
    status = status_match.group(1).strip()
    if status not in VALID_STATUSES:
        raise TaskParseError(
            f"Invalid status {status!r} in {file_path}. "
            f"Expected one of {sorted(VALID_STATUSES)}."
        )

    assignee_match = _ASSIGNEE_PATTERN.search(text)
    if assignee_match is None:
        raise TaskParseError(f"Assignee not found in {file_path}")
    assignee = assignee_match.group(1).strip()

    deps_section = _extract_section(text, _DEPENDENCIES_HEADER)
    dependencies: List[str] = []
    for line in deps_section.splitlines():
        line = line.strip()
        if line.startswith("- ") and not line.startswith("- None"):
            dep = line[2:].strip()
            if dep and _TASK_ID_FORMAT.match(dep):
                dependencies.append(dep)

    owned_section = _extract_section(text, _OWNED_SURFACE_HEADER)
    owned_surface: List[str] = []
    in_item = False
    for line in owned_section.splitlines():
        stripped = line.strip()
        if stripped.startswith("- "):
            in_item = not stripped.startswith("- Bu görev")
            line_source = stripped
        elif in_item and "`" in stripped:
            # Continuation of the previous bullet: wrapped backtick
            # fragments belong to the same Owned surface item.
            line_source = stripped
        else:
            in_item = False
            continue
        for p in _BACKTICK_PATH.findall(line_source):
            fragment = p.strip()
            # Only path-shaped fragments (contain a separator, dot, or
            # glob character) enter the allowlist. Prose, task IDs and
            # other backticked words in the Owned surface section are
            # ignored so free-text lines cannot widen the write set.
            if _PATH_SHAPE.search(fragment):
                owned_surface.append(fragment)

    return TaskMetadata(
        task_id=task_id,
        status=status,
        assignee=assignee,
        dependencies=dependencies,
        owned_surface=owned_surface,
        file_path=file_path,
    )


def find_task_file(task_id: str, plan_dir: Path = PLAN_DIR) -> Path:
    """Find the Markdown file for a given Task ID by searching plan/ recursively."""
    for md_file in plan_dir.rglob("*.md"):
        text = md_file.read_text(encoding="utf-8")
        matches = _TASK_ID_PATTERN.findall(text)
        if matches and matches[0].strip() == task_id:
            return md_file
    raise TaskParseError(f"Task file for {task_id} not found under {plan_dir}")


def check_dependency_status(
    dep_id: str, plan_dir: Path = PLAN_DIR
) -> Tuple[bool, str]:
    """Check whether a dependency task is Done.

    Returns (is_done, status_or_reason).
    """
    try:
        dep_file = find_task_file(dep_id, plan_dir)
    except TaskParseError:
        return False, f"Dependency file not found: {dep_id}"
    dep_meta = parse_task_file(dep_file)
    if dep_meta.status != "Done":
        return False, dep_meta.status
    return True, "Done"


# ---------------------------------------------------------------------------
# Path normalisation and glob matching
# ---------------------------------------------------------------------------

_TRAVERSAL_PATTERN = re.compile(r"(?:^|/)\.\.(?:/|$)")


def normalize_path(raw: str) -> str:
    """Normalise a path for comparison.

    - Converts backslashes to forward slashes.
    - Lowercases the entire path (Windows is case-insensitive).
    - Strips leading ``./``.
    """
    path = raw.replace("\\", "/")
    if path.startswith("./"):
        path = path[2:]
    return path.lower()


def contains_traversal(path: str) -> bool:
    """Return True if the path contains a ``..`` directory-traversal segment."""
    return bool(_TRAVERSAL_PATTERN.search(path))


def glob_to_regex(pattern: str) -> str:
    """Convert a glob pattern to a regex pattern.

    Supported glob features:
    - ``**`` matches any sequence of characters including ``/``.
    - ``*`` matches any sequence of characters except ``/``.
    - ``?`` matches a single character except ``/``.
    - All other characters are escaped literally.
    """
    result: List[str] = []
    i = 0
    while i < len(pattern):
        char = pattern[i]
        if char == "*":
            if i + 1 < len(pattern) and pattern[i + 1] == "*":
                result.append(".*")
                i += 2
                if i < len(pattern) and pattern[i] == "/":
                    result.append("/")
                    i += 1
            else:
                result.append("[^/]*")
                i += 1
        elif char == "?":
            result.append("[^/]")
            i += 1
        else:
            result.append(re.escape(char))
            i += 1
    return "^" + "".join(result) + "$"


def path_matches(path: str, patterns: List[str]) -> bool:
    """Return True if *path* matches any of the glob *patterns*."""
    for pattern in patterns:
        regex = glob_to_regex(pattern)
        if re.match(regex, path):
            return True
    return False


# ---------------------------------------------------------------------------
# Git state inspection
# ---------------------------------------------------------------------------

class GitChange:
    """A single changed path in the Git worktree."""

    def __init__(
        self,
        path: str,
        change_type: str,
        old_path: Optional[str] = None,
    ) -> None:
        self.path = path
        self.change_type = change_type
        self.old_path = old_path

    def all_paths(self) -> List[str]:
        """Return all paths that need to be checked against the allowlist."""
        paths = [self.path]
        if self.old_path is not None:
            paths.append(self.old_path)
        return paths

    def to_dict(self) -> Dict[str, str]:
        result: Dict[str, str] = {
            "path": self.path,
            "change_type": self.change_type,
        }
        if self.old_path is not None:
            result["old_path"] = self.old_path
        return result


def get_git_changes(repo_root: Path) -> List[GitChange]:
    """Return all changed paths in the Git worktree.

    Uses ``git status --porcelain=v1`` which reports staged, unstaged,
    untracked, deleted, and renamed paths in a single call.
    """
    result = subprocess.run(
        [
            "git", "-c", "core.quotePath=false", "-C", str(repo_root),
            "status", "--porcelain=v1", "--renames", "-uall",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
    )
    changes: List[GitChange] = []
    for line in result.stdout.splitlines():
        if not line:
            continue
        xy = line[:2]
        rest = line[3:]

        if " -> " in rest:
            old_path, new_path = rest.split(" -> ", 1)
            change_type = "renamed"
            changes.append(
                GitChange(
                    path=normalize_path(new_path),
                    change_type=change_type,
                    old_path=normalize_path(old_path),
                )
            )
        else:
            path = normalize_path(rest)
            if xy == "??":
                change_type = "untracked"
            elif xy[0] == "A" or xy[1] == "A":
                change_type = "added"
            elif xy[0] == "D" or xy[1] == "D":
                change_type = "deleted"
            elif xy[0] == "R" or xy[1] == "R":
                change_type = "renamed"
            else:
                change_type = "modified"
            changes.append(GitChange(path=path, change_type=change_type))

    return changes


def get_git_diff_changes(repo_root: Path, base_ref: str) -> List[GitChange]:
    """Return all paths changed in commits reachable from HEAD but not from
    the merge-base with *base_ref*.

    Uses ``git diff --name-status <base_ref>...HEAD`` so a fresh checkout
    with a clean worktree still yields the PR/branch change set. Renames are
    reported with both old and new paths.
    """
    result = subprocess.run(
        [
            "git", "-c", "core.quotePath=false", "-C", str(repo_root),
            "diff", "--name-status", "--find-renames",
            f"{base_ref}...HEAD",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
    )
    changes: List[GitChange] = []
    for line in result.stdout.splitlines():
        if not line:
            continue
        parts = line.split("\t")
        status = parts[0]
        if status.startswith(("R", "C")):
            changes.append(
                GitChange(
                    path=normalize_path(parts[2]),
                    change_type="renamed",
                    old_path=normalize_path(parts[1]),
                )
            )
        elif status.startswith("D"):
            changes.append(
                GitChange(path=normalize_path(parts[1]), change_type="deleted")
            )
        elif status.startswith("A"):
            changes.append(
                GitChange(path=normalize_path(parts[1]), change_type="added")
            )
        else:
            changes.append(
                GitChange(path=normalize_path(parts[1]), change_type="modified")
            )

    return changes


# ---------------------------------------------------------------------------
# Allowlist construction and validation
# ---------------------------------------------------------------------------

def build_allowlist(
    task: TaskMetadata, workspace: Path = WORKSPACE
) -> List[str]:
    """Build the write allowlist for a task.

    The allowlist includes:
    - All patterns from the task's ``Owned surface`` section.
    - The task's own metadata file path.
    - ``evidence/<Task-ID>/**``.
    """
    patterns: List[str] = []
    for surface in task.owned_surface:
        normalized = normalize_path(surface)
        patterns.append(normalized)

    metadata_path = normalize_path(
        str(task.file_path.relative_to(workspace))
    )
    patterns.append(metadata_path)

    patterns.append(f"evidence/{task.task_id.lower()}/**")

    return patterns


def validate_changes(
    changes: List[GitChange],
    allowlist: List[str],
) -> List[Dict[str, str]]:
    """Validate all Git changes against the allowlist.

    Returns a list of findings for paths that are out of scope.
    Each finding is a dict with keys: path, change_type, reason.
    """
    findings: List[Dict[str, str]] = []
    for change in changes:
        for path in change.all_paths():
            if contains_traversal(path):
                findings.append(
                    {
                        "path": path,
                        "change_type": change.change_type,
                        "reason": "path traversal detected",
                    }
                )
                continue
            if not path_matches(path, allowlist):
                findings.append(
                    {
                        "path": path,
                        "change_type": change.change_type,
                        "reason": "path not in task allowlist",
                    }
                )
    return findings


def validate_task_metadata(
    task: TaskMetadata, plan_dir: Path = PLAN_DIR
) -> List[str]:
    """Validate task metadata and return a list of error messages.

    Checks:
    - Status is InProgress or Done (not Planned or Blocked).
    - Assignee is set and not generic.
    - All dependencies are Done.
    """
    errors: List[str] = []

    if task.status not in ("InProgress", "Done"):
        errors.append(
            f"Task status is {task.status!r}, expected 'InProgress' or 'Done'"
        )

    assignee_lower = task.assignee.lower().strip()
    if (
        not task.assignee
        or assignee_lower == "codex"
        or assignee_lower == "ai"
        or assignee_lower == "none"
        or assignee_lower.startswith("unassigned")
    ):
        errors.append(
            f"Assignee is {task.assignee!r}, must be a specific session ID"
        )

    for dep_id in task.dependencies:
        is_done, reason = check_dependency_status(dep_id, plan_dir)
        if not is_done:
            errors.append(
                f"Dependency {dep_id} is not Done (status: {reason})"
            )

    return errors


# ---------------------------------------------------------------------------
# Main entry point
# ---------------------------------------------------------------------------

def run_validation(
    task_id: str,
    repo_root: Path,
    plan_dir: Path,
    diff_base: Optional[str] = None,
) -> Dict:
    """Run the full validation and return a result dict.

    *diff_base* selects diff mode (committed changes vs worktree). When
    *diff_base* is None the current worktree is inspected.
    """
    result: Dict = {
        "task_id": task_id,
        "valid": False,
        "metadata_errors": [],
        "findings": [],
    }

    try:
        task_file = find_task_file(task_id, plan_dir)
    except TaskParseError as exc:
        result["metadata_errors"].append(str(exc))
        return result

    try:
        task = parse_task_file(task_file)
    except TaskParseError as exc:
        result["metadata_errors"].append(str(exc))
        return result

    if task.task_id != task_id:
        result["metadata_errors"].append(
            f"Task ID mismatch: expected {task_id}, found {task.task_id}"
        )
        return result

    metadata_errors = validate_task_metadata(task, plan_dir)
    result["metadata_errors"] = metadata_errors

    allowlist = build_allowlist(task, workspace=repo_root)
    if diff_base is not None:
        changes = get_git_diff_changes(repo_root, diff_base)
    else:
        changes = get_git_changes(repo_root)
    findings = validate_changes(changes, allowlist)
    result["findings"] = findings

    result["valid"] = len(metadata_errors) == 0 and len(findings) == 0
    return result


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Enforce Codex task write boundaries."
    )
    parser.add_argument(
        "--task-id",
        required=True,
        help="The active Task ID (e.g. V1-FND-003).",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=WORKSPACE,
        help="Path to the Git repository root.",
    )
    parser.add_argument(
        "--plan-dir",
        type=Path,
        default=PLAN_DIR,
        help="Path to the plan/ directory.",
    )
    parser.add_argument(
        "--format",
        choices=["json", "text"],
        default="json",
        help="Output format (default: json).",
    )
    parser.add_argument(
        "--diff-base",
        default=None,
        help="Base ref for committed-diff mode (e.g. a PR base SHA or "
        "origin/master). When set, changed paths come from "
        "'git diff --name-status <base>... HEAD' instead of the worktree.",
    )
    args = parser.parse_args(argv)

    result = run_validation(
        args.task_id,
        args.repo_root,
        args.plan_dir,
        diff_base=args.diff_base,
    )

    if args.format == "json":
        print(json.dumps(result, indent=2))
    else:
        if result["valid"]:
            print(f"OK: All changes within scope for {args.task_id}")
        else:
            print(f"FAIL: Scope violations for {args.task_id}")
            for err in result["metadata_errors"]:
                print(f"  METADATA ERROR: {err}")
            for finding in result["findings"]:
                print(
                    f"  FINDING: {finding['path']} "
                    f"({finding['change_type']}) — {finding['reason']}"
                )

    return 0 if result["valid"] else 1


if __name__ == "__main__":
    sys.exit(main())
