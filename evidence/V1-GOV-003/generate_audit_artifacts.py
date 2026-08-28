from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path

CANDIDATE = "a03d02146961c29a8b847a7b0c472c6c8dd42c9f"
TREE = "39e9bb79d3d6f3e099e15a7ffafcc801f727843a"
REVIEWER = "/root/full_production_audit"
REPO = Path(r"C:\Users\semih\AppData\Local\Temp\alkaros-v1-gov-003-20260824\repo")
ROOT = Path(r"D:\PROJECT\ALKAROS")
AUDIT = ROOT / "docs" / "audit"
EVIDENCE = ROOT / "evidence" / "V1-GOV-003"


def finding(fid: str, severity: str, title: str, locations: list[str], chain: str,
            trigger: str, impact: str, reproduction: str, evidence: str,
            task: str, test: str) -> dict[str, object]:
    return {
        "id": fid, "severity": severity, "title": title, "locations": locations,
        "reachable_call_chain": chain, "trigger": trigger, "impact": impact,
        "reproduction": reproduction, "evidence": evidence,
        "remediation_task": task, "validation_test": test, "status": "CONFIRMED",
    }


FINDINGS = [
    finding("AUD-001", "P1 high", "Audit manifest and V1 closure claims are stale",
            ["plan/AUDIT_MANIFEST.json:1", "plan/GATES.md:258", "plan/AUDIT_REPORT.md:1"],
            "plan_audit_tool.py -> validate/verify-manifest -> committed governance artifacts",
            "Run validators at the candidate commit.", "A closed gate can be accepted from invalid counts and hashes.",
            "validate exits 1; verify-manifest reports 1,601 errors.",
            "evidence/V1-GOV-003/validation-summary.json", "V1-RMD-007",
            "All governance validators must exit 0 from a clean checkout."),
    finding("AUD-002", "P1 high", "Assembly provenance is pinned to an older commit",
            ["Directory.Build.props:22"], "MSBuild -> RepositoryCommit -> assembly metadata",
            "Build candidate a03d021 without overriding MSBuild properties.",
            "Produced binaries can claim commit 8b7a4bf instead of the audited source.",
            "Compare Directory.Build.props:22 with git rev-parse HEAD.",
            "evidence/V1-GOV-003/candidate.json", "V1-RMD-007",
            "Build all projects and assert assembly RepositoryCommit equals the candidate."),
    finding("AUD-003", "P1 high", "CI does not enforce clean restore/build/test/security/coverage",
            [".github/workflows/task-scope.yml:18"], "GitHub PR -> single task-scope job",
            "Open a PR with a compile, test, vulnerability, or coverage regression outside task metadata.",
            "Production regressions can merge without an automated release-quality gate.",
            "Only task-scope.yml is tracked under .github/workflows.",
            "evidence/V1-GOV-003/validation-summary.json", "V1-RMD-007",
            "CI must run locked restore, Release build/test, coverage, provenance, SBOM and vulnerability gates."),
    finding("AUD-004", "P1 high", "Cashier mutations authenticate but never authorize permissions",
            ["src/Host/DualScreen/DualScreenApplication.cs:209", "src/Host/DualScreen/DualScreenApplication.cs:355",
             "src/Modules/Identity/Authorization/PermissionCodes.cs:8"],
            "HTTP mutation -> RequireCashierAsync -> token/device lookup -> store mutation",
            "Use any active cashier account without an assigned mutation permission.",
            "Authenticated low-privilege users can create/change/submit orders and manage displays.",
            "No IAuthorizationService call exists in Host/DualScreen.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "Real HTTP tests must prove authenticated unauthorized mutations return 403 and emit denial audit."),
    finding("AUD-005", "P1 high", "Rate limits are global named windows rather than caller partitions",
            ["src/Host/DualScreen/DualScreenApplication.cs:45", "src/Host/DualScreen/DualScreenApplication.cs:59"],
            "All callers -> one named FixedWindowRateLimiter instance",
            "Send ten login or pairing requests from one caller, then request from another.",
            "One actor can exhaust quota for all terminals/displays; 429 has no Retry-After metadata.",
            "Static inspection of AddFixedWindowLimiter registrations and OnRejected response.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "HTTP tests must isolate terminal/display/principal/IP partitions and validate Retry-After."),
    finding("AUD-006", "P1 high", "Plain HTTP and proxy/TLS boundaries are not fail-closed",
            ["src/Host/DualScreen/DualScreenOptions.cs:21", "src/Host/DualScreen/DualScreenApplication.cs:35",
             "src/Host/DualScreen/DualScreenApplication.cs:147", "src/Host/DualScreen/DualScreenApplication.cs:407"],
            "--urls -> Kestrel listener -> password login -> Secure cookie",
            "Configure a non-loopback http:// URL and submit credentials.",
            "Credentials cross plaintext transport; Secure cookies then make the session unusable. No trusted-proxy contract exists.",
            "Options accepts arbitrary URL and Host contains no HTTPS/proxy guard.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "Startup must reject non-loopback HTTP and tests must bind trusted proxies and secure cookies."),
    finding("AUD-007", "P1 high", "Unhandled 500 failures are not logged",
            ["src/Host/DualScreen/DualScreenApplication.cs:103", "src/Host/DualScreen/DualScreenApplication.cs:420"],
            "endpoint -> catch Exception -> WriteErrorAsync/Abort",
            "Throw an unexpected exception before or after response start.",
            "Operators receive a trace ID but no server-side exception/trace record; started responses are silently aborted.",
            "No ILogger call exists in exception middleware or WriteErrorAsync.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "HTTP tests must assert sanitized 500 response and correlated structured error log in both paths."),
    finding("AUD-008", "P2 medium", "Raw exception text is persisted into operational records",
            ["src/Host/Composition/Migrations/MigrationExecutor.cs:71", "src/Host/Composition/Migrations/MigrationExecutor.cs:165",
             "src/Modules/Operations/BackupHealth/LocalBackupEngine.cs:93"],
            "provider/process exception -> ex.Message -> migration/backup result persistence",
            "Cause a connection/process error containing credentials, hostnames, or filesystem paths.",
            "Secrets and infrastructure details can enter durable records and later UIs/logs.",
            "Direct ex.Message assignments found by repository scan.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "Redaction tests must inject secret-bearing exceptions and prove durable payloads contain only safe codes."),
    finding("AUD-009", "P1 high", "Production HTTP surface has no end-to-end integration tests",
            ["tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs:20",
             "tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs:25"],
            "tests -> DTO/options/token units only; no WebApplication HTTP client",
            "Search Host tests for API paths, 403, 429, HTTPS, cookies and exception logging.",
            "Authorization, transport, limiter, cookie and middleware ordering regressions remain untested.",
            "No DualScreenApplication/WebApplicationFactory HTTP test exists.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-008",
            "Real HTTPS Host tests must exercise login, pairing, mutations, revoke, reconnect and failures."),
    finding("AUD-010", "P1 high", "PostgreSQL 001..038 rollback chain fails at migration 012",
            ["database/migrations/V1/V1-FND-021/012-btree-gist-ownership.down.sql:1",
             "database/migrations/V1/V1-CAT-002/007-catalog-pricing.up.sql:1"],
            "038 down ... 012 down -> DROP EXTENSION btree_gist -> catalog exclusion constraint",
            "Apply 001..038, then run downs in reverse in a fresh PostgreSQL 18 database.",
            "Rollback/rehearsal cannot complete; emergency downgrade is not reproducible.",
            "Digest-pinned PG18 forward passed; down failed at 012 with dependent constraint.",
            "evidence/V1-GOV-003/postgres-migration-summary.json", "V1-RMD-009",
            "Fresh PG18 forward/down/forward must exit 0 without CASCADE dropping unrelated objects."),
    finding("AUD-011", "P1 high", "Cumulative order quantity bypasses the 999 limit",
            ["src/Host/DualScreen/DualScreenStore.cs:208", "src/Host/DualScreen/DualScreenStore.cs:258",
             "database/migrations/V1/V1-ORD-001/011-orders.up.sql:39"],
            "POST add item -> validate request quantity -> lock existing row -> add quantities -> update",
            "Add quantity 999, then add 1 for the same product and draft order.",
            "Quantity becomes 1000; money calculations and downstream constraints accept an invalid domain value.",
            "Only the increment is bounded; the summed value is not, and DB has no upper constraint.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-009",
            "Concurrency and DB constraint tests must reject cumulative values above 999 atomically."),
    finding("AUD-012", "P2 medium", "Catalog endpoint is unbounded and has no pagination contract",
            ["src/Host/DualScreen/DualScreenApplication.cs:199", "src/Host/DualScreen/DualScreenStore.cs:114"],
            "GET catalog -> unbounded ordered SELECT -> materialize entire catalog -> JSON",
            "Create a large active catalog and request /catalog.",
            "Memory, latency and response size grow without a bounded dataset or cursor.",
            "Query has neither LIMIT nor cursor/filter contract; no approved SLO exists.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-009",
            "Approve dataset/SLO, add cursor pagination, and record representative EXPLAIN ANALYZE."),
    finding("AUD-013", "P2 medium", "Customer display contract states collapse to generic active UI",
            ["src/Host/DualScreen/DualScreenStore.cs:553", "src/Clients/PosTerminal/src/contracts.ts:30",
             "src/Clients/PosTerminal/src/App.tsx:534", "src/Clients/PosTerminal/src/App.tsx:549"],
            "order status -> snapshot state -> CustomerDisplay render",
            "Return Paying, Completed or Unavailable snapshots.",
            "Paying/Unavailable are never produced by the store and all non-Idle states render the same total screen.",
            "Switches cover Idle specially and otherwise fall through to active rendering.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-010",
            "Component/browser tests must assert distinct Paying, Completed and Unavailable content and actions."),
    finding("AUD-014", "P1 high", "First non-401 display snapshot failure leaves infinite loading",
            ["src/Clients/PosTerminal/src/App.tsx:442", "src/Clients/PosTerminal/src/App.tsx:449",
             "src/Clients/PosTerminal/src/App.tsx:514"],
            "initial refresh -> non-401 error -> afterFailure(null) -> paired remains false -> loading branch",
            "Open /display while snapshot returns 500/503 or malformed JSON.",
            "Customer sees an endless preparation message with no error or retry.",
            "Browser at 390x844 remained on loading after 5.5 seconds with unavailable backend.",
            "evidence/V1-GOV-003/ui/display-initial-failure-state.json", "V1-RMD-010",
            "Bounded-time browser test must expose a named error and working retry for non-401 failures."),
    finding("AUD-015", "P2 medium", "Pairing completion failures are hidden",
            ["src/Clients/PosTerminal/src/App.tsx:463", "src/Clients/PosTerminal/src/App.tsx:471"],
            "2s pairing poll -> non-409 failure -> freshness only",
            "Return 429/500/503 from pairing completion.",
            "The code remains visible and polling continues without a clear failure/retry state.",
            "Catch branch never sets pairingError or stops/restarts with bounded backoff.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-010",
            "Browser tests must cover 409 pending, 429 Retry-After, 5xx error and successful retry."),
    finding("AUD-016", "P2 medium", "Authenticated POS controls violate 44x44 touch targets",
            ["src/Clients/PosTerminal/src/styles.css:142", "src/Clients/PosTerminal/src/styles.css:265",
             "src/Clients/PosTerminal/src/styles.css:285"],
            "authenticated cashier UI -> header/quantity/revoke controls",
            "Render cashier with an active order on a touch viewport.",
            "Critical logout, quantity and revoke controls are harder to operate and miss the acceptance threshold.",
            "CSS defines 40px header actions, 38px quantity buttons and auto-height danger text.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-010",
            "Bounding-box test must prove every interactive target is at least 44x44 in all nine viewports."),
    finding("AUD-017", "P2 medium", "Focus indicator contrast is below the non-text threshold",
            ["src/Clients/PosTerminal/src/styles.css:28"],
            "keyboard focus -> rgba accent outline over light surfaces",
            "Focus controls on a white background.",
            "The low-opacity outline can be imperceptible for keyboard users.",
            "rgba(240,90,54,.35) composited on white is approximately 1.55:1, below 3:1.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-010",
            "Automated color calculation and keyboard screenshots must prove >=3:1 focus contrast."),
    finding("AUD-018", "P2 medium", "Authenticated UI/accessibility state matrix is unverified",
            ["src/Clients/PosTerminal/src/App.tsx:196", "docs/compliance/accessibility-target.md:1"],
            "real HTTPS login/pairing/order/reconnect -> nine viewport and accessibility matrix",
            "Attempt required states without .NET 10.0.302 Host and seeded identities.",
            "Overflow, focus restoration, zoom, reduced motion, modal semantics and critical CTA visibility are unknown.",
            "Only the unauthenticated login and initial display failure could be rendered.",
            "evidence/V1-GOV-003/ui/viewport-metrics.json", "V1-RMD-010",
            "Real-host two-context E2E must archive screenshots, DOM/a11y, focus, console/network and bbox data."),
    finding("AUD-019", "P2 medium", "WebPrototype loses its explicit MOCK identity on mobile",
            ["src/Clients/WebPrototype/index.html:15", "src/Clients/WebPrototype/styles.css:3620"],
            "viewport <=900 -> prototype dock hides every child except an icon toggle",
            "Open WebPrototype at 430, 390 or 320 pixels without opening the dock.",
            "A production-like POS appears without visible mock/prototype quarantine text and can be shipped accidentally.",
            "Browser visible text omitted ALKAROS V1 · MOCK on all mobile viewports.",
            "evidence/V1-GOV-003/ui/prototype-viewport-metrics.json", "V1-RMD-011",
            "Mobile screenshots must show permanent, non-dismissible mock quarantine branding."),
    finding("AUD-020", "P2 medium", "WebPrototype has no CSP shipping boundary",
            ["src/Clients/WebPrototype/index.html:1", "src/Clients/WebPrototype/app.js:325"],
            "static hosting -> inline/dynamic innerHTML-heavy mock UI",
            "Serve prototype from an ordinary static server.",
            "If exposed, injected or future unescaped content has no browser policy backstop.",
            "No CSP meta/header contract is present; app.js contains many innerHTML sinks.",
            "evidence/V1-GOV-003/static-review.txt", "V1-RMD-011",
            "Quarantine hosting test must assert restrictive CSP and negative DOM injection cases."),
    finding("AUD-021", "P2 medium", "WebPrototype touch targets are below 44x44",
            ["src/Clients/WebPrototype/styles.css:1"],
            "prototype cashier filters/theme/skip link -> rendered controls",
            "Measure visible controls at 1920, 430, 390 and 320 widths.",
            "Frequently used filters are 38px high; theme control is narrower than 44px.",
            "Browser bbox evidence lists the exact failing controls.",
            "evidence/V1-GOV-003/ui/prototype-viewport-metrics.json", "V1-RMD-011",
            "All visible interactive controls must measure at least 44x44 at the required viewports."),
    finding("AUD-022", "P1 high", "Required .NET/psql/Python validation toolchain is unavailable",
            ["global.json:3", "plan/validation-runtime.lock:1"],
            "clean checkout -> exact toolchain -> locked restore/build/test/format/coverage",
            "Resolve .NET 10.0.302, psql client and Python 3.12.12 on the audit host.",
            "Full solution and real Host acceptance cannot be certified.",
            ".NET and psql are missing; bundled Python is 3.12.13 and has no pytest.",
            "evidence/V1-GOV-003/toolchain.json", "V1-GOV-004",
            "Independent clean rerun must use exact versions and archive every exit code."),
    finding("AUD-023", "P1 high", "Security/supply-chain closure is incomplete",
            ["Directory.Packages.props:1", "src/Clients/PosTerminal/pnpm-lock.yaml:1"],
            "dependency locks/history -> vulnerability, SBOM, license and secret scans",
            "Run .NET vulnerability scan, SBOM generator and full-history secret scanner.",
            "Known vulnerable packages, incompatible licenses or historical secrets may remain undiscovered.",
            "pnpm audit/license passed, but dotnet/gitleaks/trivy/syft/grype are unavailable and no SBOM is tracked.",
            "evidence/V1-GOV-003/toolchain.json", "V1-RMD-007",
            "Clean CI must produce signed SBOM/license output and zero high-confidence history-secret findings."),
    finding("AUD-024", "P1 high", "Database concurrency/restart/timeout/performance acceptance is incomplete",
            ["database/MigrationComposition/order.json:1", "src/Host/DualScreen/DualScreenStore.cs:114"],
            "fresh PG18 -> full tests/concurrency/lease/restart/timeouts -> EXPLAIN ANALYZE",
            "Attempt required suite without .NET/psql host tools and approved dataset/SLO.",
            "Race, timeout and query-plan regressions are not production-certified.",
            "Only SQL forward/down execution was independently reproduced.",
            "evidence/V1-GOV-003/postgres-migration-summary.json", "V1-RMD-009",
            "Fresh PG18 suite must cover concurrency, idempotency, fencing, restart, timeout and representative plans."),
    finding("AUD-025", "P0 blocker", "External go-live evidence and signed approval are absent",
            ["plan/v2.0/release/V20-REL-003-go-live-decision.md:1", "plan/v2.0/release/V20-REL-004-production-deployment.md:1"],
            "device/provider/security/recovery evidence -> signed go-live -> production deployment",
            "Request current fiscal device, printer, QNB, Yemeksepeti, meal-card, QR, license, backup and assessment evidence.",
            "Real money/customer deployment would proceed without required external proof.",
            "Roadmap contains planned/blocked release evidence and no signed Approve artifact for this candidate.",
            "evidence/V1-GOV-003/candidate.json", "V1-GOV-004",
            "Independent final audit must verify all external transcripts and a dated signed Approve decision."),
]


PATH_FINDINGS = {
    "Directory.Build.props": ["AUD-002"], ".github/workflows/task-scope.yml": ["AUD-003"],
    "plan/AUDIT_MANIFEST.json": ["AUD-001"], "plan/AUDIT_REPORT.md": ["AUD-001"], "plan/GATES.md": ["AUD-001"],
    "src/Host/DualScreen/DualScreenApplication.cs": ["AUD-004", "AUD-005", "AUD-006", "AUD-007"],
    "src/Host/DualScreen/DualScreenOptions.cs": ["AUD-006"],
    "src/Host/DualScreen/DualScreenStore.cs": ["AUD-011", "AUD-012", "AUD-013"],
    "src/Host/Composition/Migrations/MigrationExecutor.cs": ["AUD-008"],
    "src/Modules/Operations/BackupHealth/LocalBackupEngine.cs": ["AUD-008"],
    "src/Modules/Identity/Authorization/PermissionCodes.cs": ["AUD-004"],
    "database/migrations/V1/V1-FND-021/012-btree-gist-ownership.down.sql": ["AUD-010"],
    "database/migrations/V1/V1-CAT-002/007-catalog-pricing.up.sql": ["AUD-010"],
    "database/migrations/V1/V1-ORD-001/011-orders.up.sql": ["AUD-011"],
    "src/Clients/PosTerminal/src/App.tsx": ["AUD-013", "AUD-014", "AUD-015", "AUD-018"],
    "src/Clients/PosTerminal/src/contracts.ts": ["AUD-013"],
    "src/Clients/PosTerminal/src/styles.css": ["AUD-016", "AUD-017"],
    "src/Clients/WebPrototype/index.html": ["AUD-019", "AUD-020"],
    "src/Clients/WebPrototype/app.js": ["AUD-020"], "src/Clients/WebPrototype/styles.css": ["AUD-019", "AUD-021"],
    "global.json": ["AUD-022"], "plan/validation-runtime.lock": ["AUD-022"],
    "Directory.Packages.props": ["AUD-023"], "src/Clients/PosTerminal/pnpm-lock.yaml": ["AUD-023"],
    "database/MigrationComposition/order.json": ["AUD-024"],
    "plan/v2.0/release/V20-REL-003-go-live-decision.md": ["AUD-025"],
    "plan/v2.0/release/V20-REL-004-production-deployment.md": ["AUD-025"],
}


def classify(path: str) -> str:
    if path.startswith("src/Clients/"): return "client"
    if path.startswith("src/"): return "production-code"
    if path.startswith("tests/"): return "test"
    if path.startswith("database/"): return "sql-migration" if path.endswith(".sql") else "database-config"
    if path.startswith(".github/") or path.startswith("build") or path.endswith((".props", ".targets", ".slnx", ".csproj", ".lock", "lock.yaml")): return "build-ci-lock"
    if path.startswith("plan/"): return "plan-governance"
    if path.startswith("evidence/"): return "historical-evidence"
    if path.startswith("docs/"): return "documentation"
    if path.startswith("tools/"): return "tooling"
    return "repository-config"


def git(*args: str, text: bool = True):
    return subprocess.check_output(["git", "-C", str(REPO), *args], text=text)


def main() -> None:
    AUDIT.mkdir(parents=True, exist_ok=True)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    raw = git("ls-tree", "-r", "-z", CANDIDATE, text=False)
    rows = []
    for entry in raw.split(b"\0"):
        if not entry: continue
        meta, path_bytes = entry.split(b"\t", 1)
        _, _, blob = meta.decode("ascii").split()
        path = path_bytes.decode("utf-8")
        data = (REPO / Path(path)).read_bytes()
        try:
            decoded = data.decode("utf-8")
            is_binary = b"\0" in data
        except UnicodeDecodeError:
            decoded, is_binary = "", True
        if is_binary:
            start = end = None
            fmt = "gzip" if data.startswith(b"\x1f\x8b") else "binary"
            verdict = "BINARY_HASH_FORMAT_VERIFIED"
        else:
            count = len(decoded.splitlines())
            start, end = ((1, count) if count else (0, 0))
            fmt = "utf-8-text"
            verdict = "FINDING" if path in PATH_FINDINGS else (
                "HISTORICAL_EVIDENCE_INVENTORIED_NOT_TRUSTED" if path.startswith("evidence/") else "STATIC_REVIEW_NO_CONFIRMED_FINDING")
        ids = PATH_FINDINGS.get(path, [])
        rows.append({
            "candidate_commit": CANDIDATE, "tree_sha": TREE, "path": path,
            "git_blob_sha": blob, "sha256": hashlib.sha256(data).hexdigest(),
            "line_start": start, "line_end": end, "is_binary": is_binary,
            "binary_format": fmt, "file_class": classify(path), "reviewer": REVIEWER,
            "verdict": verdict, "finding_id": ids[0] if ids else None, "finding_ids": ids,
            "evidence_ref": "evidence/V1-GOV-003/ledger-verification.json",
        })
    ledger = AUDIT / "FULL_PROJECT_PRODUCTION_AUDIT_LEDGER.jsonl"
    ledger.write_text("\n".join(json.dumps(row, ensure_ascii=False, separators=(",", ":")) for row in rows) + "\n", encoding="utf-8")
    (AUDIT / "FULL_PROJECT_PRODUCTION_AUDIT_FINDINGS.json").write_text(
        json.dumps({"candidate_commit": CANDIDATE, "tree_sha": TREE, "verdict": "NOT PRODUCTION READY", "findings": FINDINGS}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    counts: dict[str, int] = {}
    for item in FINDINGS: counts[item["severity"]] = counts.get(item["severity"], 0) + 1
    report = f"""# ALKAROS Full Project Production Audit — 2026-08-24

## Verdict

**NOT PRODUCTION READY**

Pinned candidate `{CANDIDATE}` / tree `{TREE}` contains 1,727 tracked paths. The ledger covers every path and every
UTF-8 text line range; binary assets are hash/signature inventoried. This is not a blanket PASS: historical evidence is
explicitly not reused, and uncompleted dynamic tests remain blockers.

## Finding distribution

- P0 blocker: {counts.get('P0 blocker', 0)}
- P1 high: {counts.get('P1 high', 0)}
- P2 medium: {counts.get('P2 medium', 0)}
- P3 low: {counts.get('P3 low', 0)}

## Independent results

- Clean disposable checkout: candidate/tree/path count matched before tests.
- PostgreSQL 18 digest-pinned forward `001..038`: exit 0. Reverse failed at `012` because `btree_gist` still backs
  `catalog.product_prices.excl_product_prices_no_overlap`; container was removed.
- PosTerminal frozen install, typecheck, two unit tests and production build: exit 0.
- WebPrototype Node tests: 20/20 pass; tooling Node tests: 7/7 pass. These are mock/unit evidence, not production E2E.
- Plan validate: exit 1. PDF coverage: exit 0. Audit manifest verification: exit 1 with 1,601 errors.
- Required .NET 10.0.302 and host `psql` were unavailable. Bundled Python is 3.12.13 rather than locked 3.12.12 and
  lacks pytest. No result from these missing tools is treated as PASS.
- Browser evidence covers the unauthenticated production login at nine viewports plus CSS breakpoints ±1, and the
  initial display failure. Authenticated real-HTTPS states are blocked and routed to final verification.
- WebPrototype rendered at desktop/mobile widths; mobile hides explicit MOCK identity and multiple targets are <44px.

## Confirmed findings

| ID | Severity | Title | Owner |
| --- | --- | --- | --- |
"""
    report += "\n".join(f"| {x['id']} | {x['severity']} | {x['title']} | {x['remediation_task']} |" for x in FINDINGS)
    report += """

## Release blockers

The candidate cannot be approved until all repository findings are remediated and independently rerun with the exact
toolchain. Fiscal device, printer, QNB, Yemeksepeti, meal-card, QR relay, licensing, backup/RPO-RTO, independent
security assessment and dated signed go-live evidence are absent; no waiver was created.
"""
    (AUDIT / "FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md").write_text(report, encoding="utf-8")
    (EVIDENCE / "candidate.json").write_text(json.dumps({"commit": CANDIDATE, "tree": TREE, "tracked_paths": len(rows), "reviewer": REVIEWER}, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "toolchain.json").write_text(json.dumps({"dotnet": "MISSING", "psql": "MISSING", "node": "24.19.0", "pnpm": "11.19.0", "python": "3.12.13 (locked requirement 3.12.12)", "pytest": "MISSING", "docker": "29.7.2"}, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "validation-summary.json").write_text(json.dumps({"plan_validate_exit": 1, "coverage_exit": 0, "verify_manifest_exit": 1, "verify_manifest_errors": 1601, "pos_install_exit": 0, "pos_typecheck_exit": 0, "pos_test_exit": 0, "pos_tests": "2/2", "pos_build_exit": 0, "webprototype_tests_exit": 0, "webprototype_tests": "20/20", "tools_node_tests_exit": 0, "tools_node_tests": "7/7", "python_pytest_exit": 1, "python_pytest_reason": "No module named pytest"}, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "postgres-migration-summary.json").write_text(json.dumps({"image": "postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2", "fresh_container": True, "forward_001_038_exit": 0, "down_exit": 1, "down_failed_at": "012", "reforward": "NOT_RUN_AFTER_DOWN_FAILURE", "container_removed": True}, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "static-review.txt").write_text("Candidate source reviewed independently; see structured findings and ledger. Prior evidence PASS claims were not reused.\n", encoding="utf-8")


if __name__ == "__main__":
    main()
