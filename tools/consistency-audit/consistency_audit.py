#!/usr/bin/env python3
"""ALKAROS consistency audit.

Zero-dependency scan that fails (exit code 1) when it finds:

1. Turkish characters in identifiers or names under database/migrations/**.
2. Turkish characters in code identifiers or comments under src/** (.cs, .ts,
   .tsx), except the allow-listed currency term "kurus"/"kuruş".
3. The English words "Catalog" or "Unknown" inside a user-facing attribute
   (aria-label=, title=, placeholder=, label text) in src/Clients/**.
4. An English role noun ("Manager", "Supervisor", "Cashier") inside a quoted
   string literal in a src/Clients/** TypeScript file. User-facing role text
   must come from the central catalog (strings.ts), which is the only file
   exempt from this check (deep-analysis finding F-7). Test files are exempt.
5. A cross-schema WRITE (UPDATE / INSERT INTO / DELETE FROM another module's
   PostgreSQL schema) inside src/Modules/<M>/**. A module owns exactly one
   schema; it may READ another module's relations for same-transaction queries
   and reconciliation, but state changes to another module's rows go through
   that module's repository contract (V0-ARC-001). The append-only 'audit'
   schema (AUD-01, DB-trigger enforced) is written by every module by design
   and is exempt.

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

# An English role noun inside a quoted string literal. Matched case-sensitively
# so identifiers/JSX element names (function Cashier, <Cashier />) are not hit.
ROLE_NOUN_RE = re.compile(r"[\"'][^\"'\n]*\b(Manager|Supervisor|Cashier)\b[^\"'\n]*[\"']")

# One PostgreSQL schema per module. A module writing another module's schema is
# a boundary violation; reads are allowed (V0-ARC-001).
MODULE_SCHEMA = {
    "Orders": "orders", "Billing": "billing", "Catalog": "catalog",
    "Kitchen": "kitchen", "Identity": "identity", "Tables": "table_mgmt",
    "Cash": "cash", "Audit": "audit", "Reconciliation": "reconciliation",
    "Reporting": "reporting", "Settings": "settings",
    "Observability": "observability", "Operations": "operations",
}
_SCHEMA_CONST_RE = re.compile(r'const\s+string\s+(\w+)\s*=\s*"(\w+)\.\w+"')
_WRITE_TARGET_RE = re.compile(
    r"\b(?:UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+(?:\{(\w+)\}|(\w+)\.)", re.IGNORECASE)

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

    modules_root = REPO_ROOT / "src" / "Modules"
    if modules_root.is_dir():
        for path in _iter_files(modules_root, (".cs",)):
            parts = path.relative_to(modules_root).parts
            own_schema = MODULE_SCHEMA.get(parts[0]) if parts else None
            if own_schema is None:
                continue
            text = path.read_text(encoding="utf-8")
            const_schema = {m.group(1): m.group(2) for m in _SCHEMA_CONST_RE.finditer(text)}
            for number, raw in enumerate(text.splitlines(), 1):
                line = raw.strip()
                if line.startswith(("//", "///", "*", "#")):
                    continue
                for const_name, literal_schema in _WRITE_TARGET_RE.findall(raw):
                    schema = literal_schema or const_schema.get(const_name)
                    # 'audit' is append-only (DB trigger, AUD-01) and written by
                    # every module by design; it is not an ownership boundary.
                    if schema and schema != own_schema and schema != "audit":
                        violations.append(
                            f"{_rel(path)}:{number}: {parts[0]} module writes the '{schema}' schema; "
                            f"state changes to another module's rows go through its contract: {line[:120]}")

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
                if (
                    is_client
                    and path.suffix in (".ts", ".tsx")
                    and path.name != "strings.ts"
                    and not path.name.endswith((".test.ts", ".test.tsx"))
                    and ROLE_NOUN_RE.search(line)
                ):
                    violations.append(f"{_rel(path)}:{number}: English role noun in a string literal; use strings.ts: {line.strip()}")

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
