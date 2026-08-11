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
import difflib
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
_BLOCKER_HEADER = re.compile(r"^##\s+Blocker\s*$", re.MULTILINE)
_NEXT_HEADER = re.compile(r"^##\s+", re.MULTILINE)
_TASK_ID_FORMAT = re.compile(r"^V\d+-[A-Z]+-\d+$")
_BACKTICK_PATH = re.compile(r"`([^`]+)`")
_PATH_SHAPE = re.compile(r"[/\\.*?]")

VALID_STATUSES: Set[str] = {
    "Planned", "InProgress", "Done", "Blocked", "NotApplicable"
}
EXECUTABLE_STATUSES: Set[str] = {"Planned", "InProgress"}

_VERSION_PATTERN = re.compile(r"^(V(?:0|1|11|12|13|14|15|20))-")
_ENTRY_GATE_BY_VERSION = {
    "V1": "V0",
    "V11": "V1",
    "V12": "V11",
    "V13": "V12",
    "V14": "V13",
    "V15": "V14",
    "V20": "V15",
}
_REMEDIATION_EXCEPTION_START = "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:START -->"
_REMEDIATION_EXCEPTION_END = "<!-- TASK_SCOPE_REMEDIATION_EXCEPTIONS:END -->"
_REMEDIATION_EXCEPTION_HEADER = (
    "| Task ID | Approval date | Source basis | Purpose | Gate closure evidence | "
    "New feature behavior |"
)
_REMEDIATION_EXCEPTION_SEPARATOR = "| --- | --- | --- | --- | --- | --- |"
_REMEDIATION_EXCEPTION_ROW = re.compile(
    r"^\|\s*`(?P<task_id>V\d+-[A-Z]+-\d+)`\s*\|\s*`(?P<approval_date>\d{4}-\d{2}-\d{2})`\s*\|\s*"
    r"`(?P<source_basis>[^`|]+)`\s*\|\s*"
    r"Verified finding remediation only\s*\|\s*Not gate closure evidence\s*\|\s*"
    r"No new feature behavior\s*\|$"
)
_C52_C53_C54_CANDIDATE_REMEDIATION_RECORDS = {
    "V1-CAT-003": ("2026-08-10", "CORR:C52"),
    "V1-FND-016": ("2026-08-10", "CORR:C52"),
    "V1-FND-017": ("2026-08-10", "CORR:C52"),
    "V1-FND-018": ("2026-08-10", "CORR:C52"),
    "V1-FND-019": ("2026-08-10", "CORR:C52"),
    "V1-FND-020": ("2026-08-10", "CORR:C52"),
    "V1-FND-021": ("2026-08-10", "CORR:C52"),
    "V1-FND-022": ("2026-08-10", "CORR:C52"),
    "V1-FND-023": ("2026-08-11", "CORR:C52;CORR:C53;CORR:C54"),
    "V1-IAM-006": ("2026-08-10", "CORR:C52"),
    "V1-IAM-007": ("2026-08-10", "CORR:C52"),
    "V1-IAM-008": ("2026-08-10", "CORR:C52"),
    "V1-IAM-009": ("2026-08-10", "CORR:C52"),
    "V1-IAM-010": ("2026-08-10", "CORR:C52"),
    "V1-IAM-011": ("2026-08-10", "CORR:C52"),
    "V1-IAM-012": ("2026-08-10", "CORR:C52"),
    "V1-IAM-013": ("2026-08-10", "CORR:C52"),
    "V1-SEC-004": ("2026-08-10", "CORR:C52"),
    "V1-SEC-005": ("2026-08-10", "CORR:C52"),
}
_C52_C53_C54_CANDIDATE_CODE_REMEDIATION_TASK_IDS = set(
    _C52_C53_C54_CANDIDATE_REMEDIATION_RECORDS
)
_DEFERRED_TASKS_START = "<!-- V0_DEFERRED_TASKS:START -->"
_DEFERRED_TASKS_END = "<!-- V0_DEFERRED_TASKS:END -->"
_DEFERRED_TASKS_HEADER = (
    "| Task ID | Approval date | Reopen stage | Required evidence | "
    "Gate closure evidence |"
)
_DEFERRED_TASKS_SEPARATOR = "| --- | --- | --- | --- | --- |"
_DEFERRED_TASKS_ROW = re.compile(
    r"^\|\s*`(?P<task_id>V0-[A-Z]+-\d+)`\s*\|\s*`(?P<approval_date>2026-08-03)`\s*\|\s*"
    r"`(?P<reopen_stage>V11|V12|V13|V14|V15|V20)`\s*\|\s*"
    r"(?P<required_evidence>[^|]+?)\s*\|\s*Not V0 gate closure evidence\s*\|$"
)
_DEFERRED_TASK_RECORDS = {
    ("V0-HUG-001", "2026-08-03", "V12", "Gerçek Hugin provider contract/erişim kanıtı"),
    ("V0-QNB-001", "2026-08-03", "V13", "Gerçek QNB provider contract/erişim kanıtı"),
    ("V0-YSP-001", "2026-08-03", "V12", "Gerçek Yapı Kredi provider contract/erişim kanıtı"),
    ("V0-MCD-001", "2026-08-03", "V12", "Gerçek meal-card provider sözleşme/onay kanıtı"),
    ("V0-PRN-001", "2026-08-03", "V14", "Gerçek yazıcı/cihaz sözleşmesi veya onay kanıtı"),
    ("V0-QRG-001", "2026-08-03", "V14", "Gerçek QR relay public kanal onay kanıtı"),
    ("V0-CMP-001", "2026-08-03", "V12", "Mali müşavir onaylı FSC/T300-QNB adisyon strateji kararı"),
    ("V0-SEC-001", "2026-08-03", "V14", "Doğrulanmış güvenlik gereksinim kaynağı/standart kanıtı"),
    ("V0-LIC-001", "2026-08-03", "V20", "Gerçek license server ve lisans sözleşmesi kanıtı"),
    ("V0-BKP-001", "2026-08-03", "V15", "Gerçek PostgreSQL 18 ikinci instance/cihaz kanıtı"),
    ("V0-BKP-002", "2026-08-03", "V15", "Gerçek yedekleme donanımı/cihaz kanıtı"),
}
_DEFERRED_TASK_IDS = {record[0] for record in _DEFERRED_TASK_RECORDS}


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
    return parse_task_text(text, file_path)


def parse_task_text(text: str, file_path: Path) -> TaskMetadata:
    """Parse task Markdown text using *file_path* for diagnostics."""

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


def _task_version(task_id: str) -> str:
    """Return the supported release version encoded in *task_id*."""
    match = _VERSION_PATTERN.match(task_id)
    if match is None:
        raise TaskParseError(f"Task ID does not have a supported version: {task_id}")
    return match.group(1)


def _all_tasks(plan_dir: Path) -> List[TaskMetadata]:
    """Return every parseable task under *plan_dir*, sorted by task ID."""
    tasks: List[TaskMetadata] = []
    for task_file in sorted(plan_dir.rglob("*.md")):
        text = task_file.read_text(encoding="utf-8")
        if not _TASK_ID_PATTERN.search(text):
            continue
        tasks.append(parse_task_text(text, task_file))
    return sorted(tasks, key=lambda item: item.task_id)


def parse_remediation_exception_records(plan_dir: Path) -> Dict[str, Tuple[str, str]]:
    """Return the exact C52/C53/C54-approved remediation records from ``GATES.md``.

    The table is deliberately strict: a malformed, duplicate, missing, or
    non-approved record cannot expand the entry-gate bypass.
    """
    gates_file = plan_dir / "GATES.md"
    if not gates_file.is_file():
        raise TaskParseError("Remediation exception table not found in GATES.md")

    lines = gates_file.read_text(encoding="utf-8").splitlines()
    starts = [
        index for index, line in enumerate(lines) if line == _REMEDIATION_EXCEPTION_START
    ]
    ends = [
        index for index, line in enumerate(lines) if line == _REMEDIATION_EXCEPTION_END
    ]
    if len(starts) != 1 or len(ends) != 1:
        raise TaskParseError("Remediation exception table markers must occur exactly once")
    start, end = starts[0], ends[0]

    if start >= end:
        raise TaskParseError("Remediation exception table markers are out of order")

    table_lines = lines[start + 1:end]
    if len(table_lines) < 3:
        raise TaskParseError("Remediation exception table is incomplete")
    if table_lines[0] != _REMEDIATION_EXCEPTION_HEADER:
        raise TaskParseError("Remediation exception table header is invalid")
    if table_lines[1] != _REMEDIATION_EXCEPTION_SEPARATOR:
        raise TaskParseError("Remediation exception table separator is invalid")

    records: Dict[str, Tuple[str, str]] = {}
    for line in table_lines[2:]:
        match = _REMEDIATION_EXCEPTION_ROW.fullmatch(line)
        if match is None:
            raise TaskParseError("Remediation exception table contains an invalid record")
        task_id = match.group("task_id")
        if task_id in records:
            raise TaskParseError(
                f"Remediation exception table contains a duplicate Task ID: {task_id}"
            )
        records[task_id] = (match.group("approval_date"), match.group("source_basis"))

    if records != _C52_C53_C54_CANDIDATE_REMEDIATION_RECORDS:
        raise TaskParseError(
            "Remediation exception table records must exactly match the "
            "C52/C53/C54 user approval"
        )
    return records


def parse_remediation_exception_ids(plan_dir: Path) -> Set[str]:
    """Return the exact candidate-remediation IDs from ``GATES.md``."""
    return set(parse_remediation_exception_records(plan_dir))


def parse_v0_deferral_ids(plan_dir: Path) -> Set[str]:
    """Return the user-approved deferred V0 task IDs from ``GATES.md``.

    The table is deliberately strict: a malformed, duplicate, missing, or
    non-approved record cannot expand or hide the entry-gate exemption.
    """
    gates_file = plan_dir / "GATES.md"
    if not gates_file.is_file():
        raise TaskParseError("V0 deferral table not found in GATES.md")

    lines = gates_file.read_text(encoding="utf-8").splitlines()
    starts = [
        index for index, line in enumerate(lines) if line == _DEFERRED_TASKS_START
    ]
    ends = [
        index for index, line in enumerate(lines) if line == _DEFERRED_TASKS_END
    ]
    if len(starts) != 1 or len(ends) != 1:
        raise TaskParseError("V0 deferral table markers must occur exactly once")
    start, end = starts[0], ends[0]

    if start >= end:
        raise TaskParseError("V0 deferral table markers are out of order")

    table_lines = lines[start + 1:end]
    if len(table_lines) < 3:
        raise TaskParseError("V0 deferral table is incomplete")
    if table_lines[0] != _DEFERRED_TASKS_HEADER:
        raise TaskParseError("V0 deferral table header is invalid")
    if table_lines[1] != _DEFERRED_TASKS_SEPARATOR:
        raise TaskParseError("V0 deferral table separator is invalid")

    records: Set[tuple] = set()
    for line in table_lines[2:]:
        match = _DEFERRED_TASKS_ROW.fullmatch(line)
        if match is None:
            raise TaskParseError("V0 deferral table contains an invalid record")
        record = (
            match.group("task_id"),
            match.group("approval_date"),
            match.group("reopen_stage"),
            match.group("required_evidence"),
        )
        if record in records:
            raise TaskParseError(
                f"V0 deferral table contains a duplicate Task ID: {record[0]}"
            )
        records.add(record)

    if records != _DEFERRED_TASK_RECORDS:
        raise TaskParseError(
            "V0 deferral table records must exactly match the 2026-08-03 "
            "user approval"
        )
    return _DEFERRED_TASK_IDS


def check_entry_gate(task: TaskMetadata, plan_dir: Path) -> List[str]:
    """Return closure errors for the release gate immediately before *task*.

    A gate is closed only when every task in the preceding release version is
    explicitly ``Done`` or ``NotApplicable``.  The plan has no authoritative
    closure flag; deriving the result from the task records prevents a mutable
    prose statement from opening a release gate.
    """
    version = _task_version(task.task_id)
    prerequisite_version = _ENTRY_GATE_BY_VERSION.get(version)
    if prerequisite_version is None:
        return []

    preceding = [
        item for item in _all_tasks(plan_dir)
        if _task_version(item.task_id) == prerequisite_version
    ]
    gate_id = f"GATE-{prerequisite_version}-EXIT"
    if not preceding:
        return [f"Entry gate {gate_id} cannot be verified: no {prerequisite_version} tasks found"]

    unfinished = [
        f"{item.task_id} ({item.status})"
        for item in preceding
        if item.status not in {"Done", "NotApplicable"}
    ]
    if unfinished:
        if task.task_id in _C52_C53_C54_CANDIDATE_CODE_REMEDIATION_TASK_IDS:
            try:
                exception_ids = parse_remediation_exception_ids(plan_dir)
            except TaskParseError as exc:
                return [
                    f"Entry gate {gate_id} remediation exception rejected: {exc}"
                ]
            if task.task_id in exception_ids:
                return []
            return [f"Entry gate {gate_id} is open: " + ", ".join(unfinished)]
        if gate_id == "GATE-V0-EXIT":
            try:
                deferred_ids = parse_v0_deferral_ids(plan_dir)
            except TaskParseError as exc:
                if not (plan_dir / "GATES.md").is_file():
                    return [f"Entry gate {gate_id} is open: " + ", ".join(unfinished)]
                return [f"Entry gate {gate_id} deferral table rejected: {exc}"]
            still_open = [
                f"{item.task_id} ({item.status})"
                for item in preceding
                if item.status not in {"Done", "NotApplicable"}
                and item.task_id not in deferred_ids
            ]
            if still_open:
                return [f"Entry gate {gate_id} is open: " + ", ".join(still_open)]
            return []
        return [f"Entry gate {gate_id} is open: " + ", ".join(unfinished)]
    return []


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

def build_allowlist(task: TaskMetadata) -> List[str]:
    """Build the write allowlist for a task.

    The allowlist includes:
    - All patterns from the task's ``Owned surface`` section.
    - ``evidence/<Task-ID>/**``.

    The task Markdown is deliberately excluded. It is validated separately
    because only its ``Status`` and ``Assignee`` metadata lines may change.
    """
    patterns: List[str] = []
    for surface in task.owned_surface:
        normalized = normalize_path(surface)
        patterns.append(normalized)

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
    task: TaskMetadata,
    plan_dir: Path = PLAN_DIR,
    allow_blocked_transition: bool = False,
    candidate_remediation: bool = False,
) -> List[str]:
    """Validate task metadata and return a list of error messages.

    Checks:
    - Status is Planned or a named active InProgress session.
    - Assignee is set and not generic.
    - All dependencies are Done.
    """
    errors: List[str] = []

    if task.status not in EXECUTABLE_STATUSES and not allow_blocked_transition:
        errors.append(
            f"Task status is {task.status!r}, expected 'Planned' or 'InProgress'"
        )

    if candidate_remediation and task.status != "InProgress":
        errors.append(
            "Candidate-code remediation task status is "
            f"{task.status!r}, expected 'InProgress'"
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

    if not candidate_remediation:
        for dep_id in task.dependencies:
            is_done, reason = check_dependency_status(dep_id, plan_dir)
            if not is_done:
                errors.append(
                    f"Dependency {dep_id} is not Done (status: {reason})"
                )

    if not candidate_remediation:
        errors.extend(check_entry_gate(task, plan_dir))

    return errors


def _git_file_text(repo_root: Path, ref: str, relative_path: str) -> Optional[str]:
    """Return UTF-8 text for *relative_path* at *ref*, or None if absent."""
    result = subprocess.run(
        ["git", "-C", str(repo_root), "show", f"{ref}:{relative_path}"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return result.stdout if result.returncode == 0 else None


def _blocker_section(text: str) -> str:
    """Return the complete Blocker section, including its heading."""
    match = _BLOCKER_HEADER.search(text)
    if match is None:
        return ""
    next_header = _NEXT_HEADER.search(text, match.end())
    end = next_header.start() if next_header is not None else len(text)
    return text[match.start():end]


def _without_mutable_task_content(text: str, allow_blocker: bool) -> str:
    """Remove the only task Markdown content permitted during a status change."""
    mutable_metadata = re.compile(r"^- (?:Status|Assignee):.*$", re.MULTILINE)
    result = mutable_metadata.sub("", text)
    if not allow_blocker:
        return result
    blocker = _blocker_section(result)
    if not blocker:
        return result
    start = result.find(blocker)
    before = result[:start].rstrip("\r\n")
    after = result[start + len(blocker):].lstrip("\r\n")
    return before + ("\n\n" + after if after else "\n")


def _is_legal_blocker_transition(baseline: TaskMetadata, current: TaskMetadata) -> bool:
    return (
        baseline.status == "Blocked" and current.status in EXECUTABLE_STATUSES
    ) or (
        baseline.status in EXECUTABLE_STATUSES and current.status == "Blocked"
    )


def validate_task_markdown_change(
    task: TaskMetadata,
    changes: List[GitChange],
    repo_root: Path,
    diff_base: Optional[str],
) -> List[Dict[str, str]]:
    """Reject task Markdown changes outside the two mutable metadata lines.

    The baseline comes from HEAD in worktree mode and from the merge-base in
    CI diff mode. A task file absent from that baseline is rejected: otherwise
    an untracked task could define its own broad Owned surface and immediately
    write outside the intended boundary.
    """
    relative_path_raw = task.file_path.relative_to(repo_root).as_posix()
    relative_path = normalize_path(relative_path_raw)
    task_change = next(
        (change for change in changes if relative_path in change.all_paths()),
        None,
    )
    if task_change is None:
        return []

    baseline_ref = "HEAD"
    if diff_base is not None:
        merge_base = subprocess.run(
            ["git", "-C", str(repo_root), "merge-base", diff_base, "HEAD"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        if merge_base.returncode != 0:
            return [{
                "path": relative_path,
                "change_type": task_change.change_type,
                "reason": "cannot resolve diff-mode task Markdown baseline",
            }]
        baseline_ref = merge_base.stdout.strip()

    baseline = _git_file_text(repo_root, baseline_ref, relative_path_raw)
    if baseline is None:
        return [{
            "path": relative_path,
            "change_type": task_change.change_type,
            "reason": "task Markdown has no committed baseline",
        }]

    if diff_base is None:
        current = task.file_path.read_text(encoding="utf-8") if task.file_path.exists() else ""
    else:
        current = _git_file_text(repo_root, "HEAD", relative_path_raw)
        if current is None:
            return [{
                "path": relative_path,
                "change_type": task_change.change_type,
                "reason": "task Markdown deletion is not permitted",
            }]

    try:
        baseline_task = parse_task_text(baseline, task.file_path)
    except TaskParseError:
        baseline_task = None

    allow_blocker = (
        baseline_task is not None
        and _is_legal_blocker_transition(baseline_task, task)
    )
    if allow_blocker:
        baseline_blocker = _blocker_section(baseline)
        current_blocker = _blocker_section(current)
        if baseline_task.status == "Blocked":
            if not baseline_blocker or current_blocker:
                return [{
                    "path": relative_path,
                    "change_type": task_change.change_type,
                    "reason": "Blocked-to-executable transition must remove Blocker",
                }]
        elif not current_blocker or "ancak" not in current_blocker.casefold():
            return [{
                "path": relative_path,
                "change_type": task_change.change_type,
                "reason": "Executable-to-Blocked transition needs an unlockable Blocker",
            }]

    baseline_fixed = _without_mutable_task_content(baseline, allow_blocker)
    current_fixed = _without_mutable_task_content(current, allow_blocker)
    if baseline_fixed == current_fixed:
        return []

    changed_lines = list(
        difflib.unified_diff(
            baseline_fixed.splitlines(), current_fixed.splitlines(), n=0
        )
    )
    return [{
        "path": relative_path,
        "change_type": task_change.change_type,
        "reason": "task Markdown changed outside Status or Assignee metadata",
        "detail": "\\n".join(changed_lines[:8]),
    }]


# ---------------------------------------------------------------------------
# Main entry point
# ---------------------------------------------------------------------------

def run_validation(
    task_id: str,
    repo_root: Path,
    plan_dir: Path,
    diff_base: Optional[str] = None,
    candidate_remediation: bool = False,
) -> Dict:
    """Run the full validation and return a result dict.

    *diff_base* selects diff mode (committed changes vs worktree). When
    *diff_base* is None the current worktree is inspected.
    """
    repo_root = repo_root.resolve()
    plan_dir = plan_dir.resolve()
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

    if diff_base is not None:
        changes = get_git_diff_changes(repo_root, diff_base)
    else:
        changes = get_git_changes(repo_root)
    try:
        task_path = normalize_path(str(task.file_path.relative_to(repo_root)))
    except ValueError:
        result["metadata_errors"].append(
            f"Task file must be within repository root: {task.file_path}"
        )
        return result
    allowlist_task = task
    baseline_task: Optional[TaskMetadata] = None
    if any(task_path in change.all_paths() for change in changes):
        baseline_ref = "HEAD"
        if diff_base is not None:
            merge_base = subprocess.run(
                ["git", "-C", str(repo_root), "merge-base", diff_base, "HEAD"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            if merge_base.returncode == 0:
                baseline_ref = merge_base.stdout.strip()
            else:
                baseline_ref = ""
        baseline = (
            _git_file_text(
                repo_root,
                baseline_ref,
                task.file_path.relative_to(repo_root).as_posix(),
            )
            if baseline_ref
            else None
        )
        if baseline is not None:
            allowlist_task = parse_task_text(baseline, task.file_path)

            baseline_task = allowlist_task

    if candidate_remediation:
        try:
            exception_ids = parse_remediation_exception_ids(plan_dir)
        except TaskParseError as exc:
            result["metadata_errors"].append(
                f"Candidate-code remediation exception rejected: {exc}"
            )
            return result
        if (
            task.task_id not in _C52_C53_C54_CANDIDATE_CODE_REMEDIATION_TASK_IDS
            or task.task_id not in exception_ids
        ):
            result["metadata_errors"].append(
                f"Task {task.task_id} is not an approved candidate-code remediation"
            )
            return result

    allow_blocked_transition = (
        task.status == "Blocked"
        and baseline_task is not None
        and baseline_task.status in EXECUTABLE_STATUSES
    )
    metadata_errors = validate_task_metadata(
        task,
        plan_dir,
        allow_blocked_transition=allow_blocked_transition,
        candidate_remediation=candidate_remediation,
    )
    result["metadata_errors"] = metadata_errors

    allowlist = build_allowlist(allowlist_task)
    non_metadata_changes = [
        change
        for change in changes
        if task_path not in change.all_paths()
    ]
    findings = validate_changes(non_metadata_changes, allowlist)
    if allow_blocked_transition:
        findings.extend(
            {
                "path": change.path,
                "change_type": change.change_type,
                "reason": "Executable-to-Blocked transition cannot write non-task paths",
            }
            for change in non_metadata_changes
        )
    findings.extend(
        validate_task_markdown_change(task, changes, repo_root, diff_base)
    )
    findings.sort(key=lambda finding: (finding["path"], finding["reason"]))
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
    parser.add_argument(
        "--candidate-remediation",
        action="store_true",
        help="Allow only a registered candidate-code remediation task to repair pre-existing evidence while its dependencies remain Blocked.",
    )
    args = parser.parse_args(argv)

    result = run_validation(
        args.task_id,
        args.repo_root,
        args.plan_dir,
        diff_base=args.diff_base,
        candidate_remediation=args.candidate_remediation,
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
