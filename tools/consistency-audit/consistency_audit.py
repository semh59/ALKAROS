#!/usr/bin/env python3
"""ALKAROS consistency audit.

Zero-dependency scan that fails (exit code 1) when it finds:

1. Turkish characters in identifiers or names under database/migrations/**.
2. Turkish characters in code identifiers or comments under src/** (.cs, .ts,
   .tsx), except the allow-listed currency term "kurus"/"kuruş".
3. The English words "Catalog" or "Unknown" inside a user-facing attribute
   (aria-label=, title=, placeholder=, label text) in src/Clients/**.

User-facing Turkish string literals are intentionally NOT flagged; only code
identities and untranslated English leaks are.

Run before every remediation wave and version gate. See
docs/CONSISTENCY_AUDIT.md.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

TURKISH_CHARS = "şğıöüçŞĞİÖÜÇ"
TURKISH_RE = re.compile(f"[{TURKISH_CHARS}]")

CODE_SUFFIXES = (".cs", ".ts", ".tsx")
COMMENT_PREFIX_RE = re.compile(r"^\s*(//|///|\*|#)")
IDENTIFIER_RE = re.compile(
    r"\b(class|interface|record|enum|struct|namespace|func|function|const|let|var|type|def)\s+"
    r"[A-Za-z_][A-Za-z0-9_]*"
)
# Currency proper noun with no English equivalent (Turkish lira minor unit).
COMMENT_ALLOW_RE = re.compile(r"kuru[sş]", re.IGNORECASE)

LEAK_RE = re.compile(
    r"(aria-label|title|placeholder)\s*=\s*[\"'][^\"']*\b(Catalog|Unknown)\b"
    r"|>\s*(Catalog|Unknown)\s+ara\b"
)

SKIP_DIR_PARTS = {"bin", "obj", "node_modules", "dist", ".git"}


def _iter_files(root: Path, suffixes: tuple[str, ...]) -> list[Path]:
    files: list[Path] = []
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix not in suffixes:
            continue
        if any(part in SKIP_DIR_PARTS for part in path.parts):
            continue
        files.append(path)
    return files


def _rel(path: Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def audit() -> list[str]:
    violations: list[str] = []

    migrations = REPO_ROOT / "database" / "migrations"
    if migrations.is_dir():
        for path in _iter_files(migrations, (".sql",)):
            for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                if TURKISH_RE.search(line):
                    violations.append(f"{_rel(path)}:{number}: Turkish character in migration: {line.strip()}")

    src = REPO_ROOT / "src"
    if src.is_dir():
        for path in _iter_files(src, CODE_SUFFIXES):
            is_client = "Clients" in path.parts
            for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
                line = raw.rstrip("\n")
                if COMMENT_PREFIX_RE.match(line):
                    if TURKISH_RE.search(line) and not COMMENT_ALLOW_RE.search(line):
                        violations.append(f"{_rel(path)}:{number}: Turkish character in comment: {line.strip()}")
                    continue
                identifier = IDENTIFIER_RE.search(line)
                if identifier and TURKISH_RE.search(identifier.group(0)):
                    violations.append(f"{_rel(path)}:{number}: Turkish character in identifier: {line.strip()}")
                if is_client and LEAK_RE.search(line):
                    violations.append(f"{_rel(path)}:{number}: untranslated English term (Catalog/Unknown) in UI attribute: {line.strip()}")

    return violations


def main() -> int:
    violations = audit()
    if violations:
        print(f"consistency-audit: {len(violations)} violation(s)")
        for item in violations:
            print(f"  {item}")
        return 1
    print("consistency-audit: clean")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
