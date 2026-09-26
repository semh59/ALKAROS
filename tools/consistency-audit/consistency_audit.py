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
   exempt from this check (deep-analysis finding F-7). Test files are exempt,
   as is a module path — a static `import ... from "..."` specifier or a
   dynamic `import("...")` call argument (V1-RMD-139) — since a file path is
   not user-facing text.
5. A cross-schema WRITE (UPDATE / INSERT INTO / DELETE FROM another module's
   PostgreSQL schema) inside src/Modules/<M>/**. A module owns exactly one
   schema; it may READ another module's relations for same-transaction queries
   and reconciliation, but state changes to another module's rows go through
   that module's repository contract (V0-ARC-001). The append-only 'audit'
   schema (AUD-01, DB-trigger enforced) is written by every module by design
   and is exempt.
6. An unbounded list read on a module's repository contract surface: a
   `public [async] Task<IReadOnlyList<...>> <Method>(` whose body issues a
   SELECT (has a `FROM`) but carries no `LIMIT`. This covers both whole-table
   `GetAll...` reads and filtered `GetByX` reads — a `WHERE` clause is not a
   bound, and a filter that widens (or a relation that outgrows its assumption)
   must fail loud instead of loading unboundedly. Signature-only interface
   declarations, one-line forwarders (no `FROM` in the body), and private
   `Read.../Load...` helpers that load one already-scoped aggregate's children
   are not flagged.
7. The same cross-schema WRITE check as rule 5, but for src/Host/** (the
   composition root has no per-module schema, but each Experience area —
   Orders, Billing, Catalog, Tables, KitchenOperations, Roles, Authorization,
   DualScreen — is still expected to write only its own bounded context's
   schema; see HOST_AREA_SCHEMA). Found missing by an independent boundary
   audit (2026-09-07): rule 5 only ever looked at src/Modules/**, so a Host
   store writing a schema it doesn't own went uncaught the same way the
   5-module MODULE_SCHEMA gap did before V11-RMD-002. The already-reconciled
   table_mgmt.tables "soft cache pointer" pattern (Orders/Billing/DualScreen
   writing a denormalized current_order_id/current_bill_id, independently
   repaired by PostgresTablePointerProjector) and the CLI/bootstrap entry
   point (Program.cs) are explicit, documented exceptions, not blind spots.

User-facing Turkish string literals are intentionally NOT flagged; only code
identities and untranslated English leaks are.

Run before every remediation wave and version gate. See
docs/CONSISTENCY_AUDIT.md.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

TURKISH_CHARS = "şğıöüçŞĞİÖÜÇ"
TURKISH_RE = re.compile(f"[{TURKISH_CHARS}]")

CODE_SUFFIXES = (".cs", ".ts", ".tsx")
COMMENT_PREFIX_RE = re.compile(r"^\s*(//|///|\*|#)")
# V1-RMD-321 (independent 2026-09-26 audit, finding K12): the character class used to stop at
# [A-Za-z0-9_] - a genuinely Turkish identifier (e.g. "var müşteriAdi") was cut off at the first Turkish
# letter, so IDENTIFIER_RE.search(line).group(0) never actually contained the Turkish character the check
# below searches for. Extended to include the Turkish alphabet's own letters so the FULL identifier is
# captured, matching the same TURKISH_CHARS this module already uses for its comment check.
IDENTIFIER_RE = re.compile(
    rf"\b(class|interface|record|enum|struct|namespace|func|function|const|let|var|type|def)\s+"
    rf"[A-Za-z_{TURKISH_CHARS}][A-Za-z0-9_{TURKISH_CHARS}]*"
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
# V1-RMD-139: a dynamic import() call's argument is a module path (e.g.
# React.lazy(() => import("./routes/Cashier"))), the exact same category the
# plain `import ... from "..."` exemption above already covers — it just
# wasn't written to recognise the dynamic call form. Exempts the whole line
# from the role-noun check the same way a static import specifier already is.
DYNAMIC_IMPORT_RE = re.compile(r"\bimport\s*\(")

# One PostgreSQL schema per module. A module writing another module's schema is
# a boundary violation; reads are allowed (V0-ARC-001).
MODULE_SCHEMA = {
    "Orders": "orders", "Billing": "billing", "Catalog": "catalog",
    "Kitchen": "kitchen", "Identity": "identity", "Tables": "table_mgmt",
    "Cash": "cash", "Audit": "audit", "Reconciliation": "reconciliation",
    "Reporting": "reporting", "Settings": "settings",
    "Observability": "observability", "Operations": "operations",
    "Recipes": "recipe", "Inventory": "inventory", "Menu": "menu",
    "Purchasing": "purchasing", "Production": "production",
    # 2026-09-22: an independent audit found these five real modules missing
    # from this dict — the same "pre-V11-RMD-002 5-module blind spot" class
    # of bug (rule 5 silently `continue`s for any module.get() miss, so none
    # of their src/Modules/**/*.cs files were ever checked for cross-schema
    # writes). Security/Support are new this session (V15-SEC-001..003,
    # V15-SUP-001); Invoicing/Payments/QrOrdering predate it and were simply
    # never added.
    "Security": "security",
    "Invoicing": "invoicing",
    "Payments": "payments",
    "QrOrdering": "qr_ordering",
}
_SCHEMA_CONST_RE = re.compile(r'const\s+string\s+(\w+)\s*=\s*"(\w+)\.\w+"')
_WRITE_TARGET_RE = re.compile(
    r"\b(?:UPDATE|INSERT\s+INTO|DELETE\s+FROM)\s+(?:\{(\w+)\}|(\w+)\.)", re.IGNORECASE)


def _find_write_targets(text: str) -> list[tuple[int, str, str, str]]:
    """Rule 5/7's own scan (V1-RMD-338, independent 2026-09-26 audit, orta seviye
    bulgu): the previous implementation ran _WRITE_TARGET_RE per line, so a raw SQL
    string that puts the verb and its schema-qualified target on separate lines
    (this codebase's own C# `\"\"\"...\"\"\"` triple-quoted literals do this
    routinely, e.g. `UPDATE\n    other_schema.table\nSET ...`) never matched at
    all - the check simply never saw it. Matching over the WHOLE file text lets
    `\\s+` cross line breaks the same way it already does within a single line.
    Returns (line_number, const_name, literal_schema, matched_line_text) for every
    match whose STARTING line is not a comment-only line (same exemption the old
    per-line loop already applied)."""
    results: list[tuple[int, str, str, str]] = []
    lines = text.splitlines()
    line_starts = [0]
    for line in lines:
        line_starts.append(line_starts[-1] + len(line) + 1)
    for match in _WRITE_TARGET_RE.finditer(text):
        line_index = 0
        for i, start in enumerate(line_starts):
            if start > match.start():
                break
            line_index = i
        line_number = line_index + 1
        raw_line = lines[line_index] if line_index < len(lines) else ""
        if raw_line.strip().startswith(("//", "///", "*", "#")):
            continue
        const_name, literal_schema = match.group(1), match.group(2)
        results.append((line_number, const_name or "", literal_schema or "", raw_line.strip()))
    return results

# src/Host/** has no per-module schema (it's the composition root), but each
# Experience area is still expected to write only its own bounded context's
# schema through ham SQL rather than another area's. Found by an independent
# boundary audit (2026-09-07): the original rule 5 never looked at src/Host/**
# at all (same class of blind spot as the pre-V11-RMD-002 5-module gap), so a
# Host store writing a schema it doesn't own would never be caught. Keyed by
# the first path segment under src/Host/Experience (a lone file directly under
# Experience/, e.g. a shared cross-cutting helper, maps to "Experience" itself
# and is treated as identity's cross-cutting surface, V0-ARC-001 row 1); the
# top-level DualScreen/ folder is its own area.
HOST_AREA_SCHEMA = {
    "Experience": "identity",
    "Experience/Authorization": "identity",
    "Experience/Billing": "billing",
    "Experience/Catalog": "catalog",
    "Experience/KitchenOperations": "kitchen",
    "Experience/NfcOrdering": "orders",
    "Experience/Orders": "orders",
    "Experience/Roles": "identity",
    "Experience/Tables": "table_mgmt",
    # V1-WTR-011: push subscriptions and the deployment's VAPID identity are
    # owned outright by this area - no module owns them, which is why the
    # migration gives them their own schema rather than borrowing identity's.
    "Experience/WebPush": "notifications",
    # V1-WTR-014: same reasoning as WebPush above - a help request is owned
    # outright by this area, no module claims it, so it gets the same
    # notifications schema rather than a fourth one of its own.
    "Experience/HelpRequests": "notifications",
    "DualScreen": "customer_display",
}

# Deliberate, already-reconciled exceptions to the one-area/one-schema rule
# above: table_mgmt.tables carries a denormalized current_order_id/
# current_bill_id "soft cache" pointer that Orders/Billing/DualScreen update
# directly for read-path speed, with PostgresTablePointerProjector (Tables
# module) independently detecting and repairing any drift — a known,
# deliberate pattern (V1-RMD-078/V1-TBL-007), not the domain-logic-duplication
# class of defect this rule exists to catch (V1-RMD-120). NfcOrdering's
# self-check-in (V14-NFC-001) is the exact same soft-cache-pointer write as
# Orders' table-draft flow, just from the unauthenticated NFC endpoint
# instead of the cashier-authenticated one.
#
# V1-WTR-013: Orders/transfer-server also writes notifications.
# serving_handoff_notes — small, ephemeral, user-directed operational
# metadata (same shape as WebPush's own push_subscriptions, which is why it
# lives in the same schema) that belongs to the hand-off ACTION itself, not
# to any order or to identity. No module owns "a note about a hand-off
# event" any more than one owns "a browser's push endpoint"; giving it a
# third schema of its own for one small table would be its own kind of
# overhead, so it borrows notifications the same deliberate way the pointer
# writes above borrow table_mgmt.
HOST_AREA_EXTRA_SCHEMAS = {
    "Experience/NfcOrdering": {"table_mgmt"},
    "Experience/Orders": {"table_mgmt", "notifications"},
    "Experience/Billing": {"table_mgmt"},
    "DualScreen": {"table_mgmt"},
}

# The CLI/bootstrap entry point (provisioning, session revocation, migration
# commands) is the composition root itself, not a per-request Experience
# store — it legitimately touches several schemas for one-off admin
# operations. Out of this rule's scope by design, not an oversight.
HOST_EXEMPT_FILES = {"Program.cs"}

# Any list read on the repository contract surface:
# `public [async] Task<IReadOnlyList<X>> <Method>(`. Its body must carry a LIMIT
# so an outgrown table — or a filter that widens — fails loud instead of loading
# unboundedly. Only bodies that actually issue a SELECT (have a FROM) are in
# scope, so signature-only declarations and one-line forwarders drop out. The
# `public` anchor keeps this on the contract boundary (a caller's filter is the
# risk); a private `Read.../Load...` helper that loads one already-scoped
# aggregate's children is bounded by that aggregate, not by a WHERE clause.
_GET_ALL_SIG_RE = re.compile(r"public\s+(?:async\s+)?Task<IReadOnlyList<[^>]+>>\s+(\w+)\s*\(")
_LIMIT_RE = re.compile(r"\bLIMIT\b", re.IGNORECASE)
# Only a body that actually issues a SELECT (has a FROM clause) is in scope; a
# one-line `return _repository.GetAll...()` forwarder is not.
_SQL_FROM_RE = re.compile(r"\bFROM\b", re.IGNORECASE)
# V1-RMD-338 (independent 2026-09-26 audit, orta seviye bulgu): _LIMIT_RE used to
# search the RAW method body, including its C# `//`/`///` comments — an
# unrelated English sentence like "// no limit needed here, the caller already
# scopes this" satisfied `\bLIMIT\b` and made the check believe a real SQL LIMIT
# clause existed, silently passing an actually-unbounded SELECT. Line comments
# are stripped before the LIMIT search only (never before the FROM search, and
# never for rule 5/7's own write-target scan) — a real SQL LIMIT always lives in
# the string literal, never in a `//` comment.
_LINE_COMMENT_RE = re.compile(r"//.*")


def _strip_line_comments(text: str) -> str:
    return "\n".join(_LINE_COMMENT_RE.sub("", line) for line in text.splitlines())

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



# 8. Reachability of DI-registered module services ("registered is not reachable",
#    V1-RMD-272). A service registered in a module (context.Register*<...>) that no
#    code path starting at src/Host/** can reach is dead in production: it may be fully
#    unit-tested and still never run. The check builds a type-name reference graph over
#    src/**/*.cs, adds one edge per DI registration (service interface -> implementation,
#    because the container resolves the implementation through the interface), and walks
#    it from every Host file. Test code never counts as a caller. A type that is
#    knowingly unreachable must be listed in unreachable_services_allowlist.json with a
#    task reference; an entry that became reachable (or stopped being registered) is
#    reported as stale so the list only ever shrinks.
UNREACHABLE_ALLOWLIST = Path(__file__).with_name("unreachable_services_allowlist.json")
_TYPE_DECL_RE = re.compile(r"\b(?:class|interface|record(?:\s+struct)?|struct|enum)\s+([A-Z][A-Za-z0-9_]*)")
_MODULE_REGISTRATION_RE = re.compile(
    r"Register(?:Singleton|Transient|Scoped)\s*<\s*([A-Za-z0-9_.]+)\s*(?:,\s*([A-Za-z0-9_.]+))?"
)
_TYPE_NAME_RE = re.compile(r"\b[A-Z][A-Za-z0-9_]*\b")


def _is_registration_file(path: Path) -> bool:
    return path.name.endswith("Module.cs") or path.name == "ModuleRegistry.cs"


def _unreachable_registered_types() -> dict[str, str]:
    """Registered module types no Host path reaches, as {type name: declaring file (repo-relative)}."""
    src_root = REPO_ROOT / "src"
    files = _iter_files(src_root, (".cs",))
    texts = {path: path.read_text(encoding="utf-8-sig", errors="replace") for path in files}

    declared_in: dict[str, set[Path]] = {}
    for path, text in texts.items():
        for match in _TYPE_DECL_RE.finditer(text):
            declared_in.setdefault(match.group(1), set()).add(path)

    references: dict[Path, set[Path]] = {}
    for path, text in texts.items():
        targets: set[Path] = set()
        for name in set(_TYPE_NAME_RE.findall(text)):
            for declaring in declared_in.get(name, ()):
                if declaring != path:
                    targets.add(declaring)
        references[path] = targets

    registered: dict[str, Path] = {}
    for path, text in texts.items():
        if not _is_registration_file(path) or "Modules" not in path.relative_to(src_root).parts[:1]:
            continue
        for match in _MODULE_REGISTRATION_RE.finditer(text):
            service = match.group(1).split(".")[-1]
            implementation = (match.group(2) or match.group(1)).split(".")[-1]
            for service_file in declared_in.get(service, ()):
                for implementation_file in declared_in.get(implementation, ()):
                    if service_file != implementation_file:
                        references.setdefault(service_file, set()).add(implementation_file)
            for name in {service, implementation}:
                for declaring in declared_in.get(name, ()):
                    registered.setdefault(name, declaring)

    roots = [
        path for path in files
        if path.relative_to(src_root).parts[:1] == ("Host",)
        and "Composition" not in path.relative_to(src_root).parts
        and not _is_registration_file(path)
    ]
    reached = set(roots)
    queue = list(roots)
    while queue:
        current = queue.pop()
        for target in references.get(current, ()):
            if target not in reached and not _is_registration_file(target):
                reached.add(target)
                queue.append(target)

    return {
        name: _rel(declaring)
        for name, declaring in registered.items()
        if all(file not in reached for file in declared_in[name])
    }


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
            for number, const_name, literal_schema, line in _find_write_targets(text):
                schema = literal_schema or const_schema.get(const_name)
                # 'audit' is append-only (DB trigger, AUD-01) and written by
                # every module by design; it is not an ownership boundary.
                if schema and schema != own_schema and schema != "audit":
                    violations.append(
                        f"{_rel(path)}:{number}: {parts[0]} module writes the '{schema}' schema; "
                        f"state changes to another module's rows go through its contract: {line[:120]}")

            # Rule 6: every list read (GetAll* or GetByX) must carry a LIMIT.
            module_lines = text.splitlines()
            for index, raw in enumerate(module_lines):
                signature = _GET_ALL_SIG_RE.search(raw)
                if signature is None:
                    continue
                # Find where the method body opens. A ';' before any '{' means an
                # interface declaration or expression-bodied forwarder — the
                # bound belongs on the concrete block body, checked there.
                start = None
                joined = "\n".join(module_lines[index:index + 8])
                brace_pos = joined.find("{")
                semi_pos = joined.find(";")
                if brace_pos == -1 or (semi_pos != -1 and semi_pos < brace_pos):
                    continue
                for probe in range(index, len(module_lines)):
                    if "{" in module_lines[probe]:
                        start = probe
                        break
                body: list[str] = []
                depth = 0
                for probe in range(start, len(module_lines)):
                    segment = module_lines[probe]
                    body.append(segment)
                    depth += segment.count("{") - segment.count("}")
                    if depth <= 0:
                        break
                body_text = "\n".join(body)
                if _SQL_FROM_RE.search(body_text) and not _LIMIT_RE.search(_strip_line_comments(body_text)):
                    violations.append(
                        f"{_rel(path)}:{index + 1}: {parts[0]} module '{signature.group(1)}' issues a SELECT "
                        f"with no LIMIT; add a bound so an outgrown table (or a widening filter) fails loud "
                        f"instead of loading unboundedly.")

    host_root = REPO_ROOT / "src" / "Host"
    if host_root.is_dir():
        for path in _iter_files(host_root, (".cs",)):
            if path.name in HOST_EXEMPT_FILES:
                continue
            parts = path.relative_to(host_root).parts
            if not parts:
                continue
            if parts[0] == "Experience":
                area = "Experience" if len(parts) == 2 else f"Experience/{parts[1]}"
            elif parts[0] == "DualScreen":
                area = "DualScreen"
            else:
                area = parts[0]
            own_schema = HOST_AREA_SCHEMA.get(area)
            allowed_extra = HOST_AREA_EXTRA_SCHEMAS.get(area, frozenset())
            text = path.read_text(encoding="utf-8")
            for number, _const_name, literal_schema, line in _find_write_targets(text):
                schema = literal_schema
                if not schema or schema == "audit" or schema == own_schema or schema in allowed_extra:
                    continue
                violations.append(
                    f"{_rel(path)}:{number}: Host area '{area}' writes the '{schema}' schema "
                    f"(expected '{own_schema or 'none mapped — update HOST_AREA_SCHEMA'}'); "
                    f"an Experience store changes another area's rows only through its module "
                    f"contract, or the documented table_mgmt pointer exception: {line[:120]}")

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
                stripped = line.lstrip()
                is_module_specifier = (
                    stripped.startswith(("import ", "export "))
                    and (" from " in stripped or stripped.startswith(("import \"", "import '")))
                ) or DYNAMIC_IMPORT_RE.search(line) is not None
                if (
                    is_client
                    and path.suffix in (".ts", ".tsx")
                    and path.name != "strings.ts"
                    and not path.name.endswith((".test.ts", ".test.tsx"))
                    and not is_module_specifier
                    and ROLE_NOUN_RE.search(line)
                ):
                    violations.append(f"{_rel(path)}:{number}: English role noun in a string literal; use strings.ts: {line.strip()}")

    violations.extend(_reachability_violations())

    return violations


def _reachability_violations() -> list[str]:
    allowlist: dict[str, str] = {}
    if UNREACHABLE_ALLOWLIST.is_file():
        allowlist = json.loads(UNREACHABLE_ALLOWLIST.read_text(encoding="utf-8"))

    unreachable = _unreachable_registered_types()
    violations: list[str] = []
    for name, declaring in sorted(unreachable.items()):
        if name not in allowlist:
            violations.append(
                f"{declaring}: {name} is DI-registered in a module but no Host code path reaches it "
                f"(add a runtime caller, or list it in {UNREACHABLE_ALLOWLIST.name} with a task reference)"
            )
    for name, reason in sorted(allowlist.items()):
        if name not in unreachable:
            violations.append(
                f"{UNREACHABLE_ALLOWLIST.name}: stale entry {name} ({reason}); it is reachable now or no longer "
                "registered - remove it"
            )
        elif not re.search(r"\bV\d+-[A-Z0-9]+-\d+\b", reason):
            violations.append(f"{UNREACHABLE_ALLOWLIST.name}: entry {name} carries no task reference")
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
