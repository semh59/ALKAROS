# Security Verification Baseline

> **Task:** V0-SEC-001
> **Status:** InProgress
> **Assignee:** codex-v0-sec-001
> **Work type:** decision
> **Source basis:** PDF:I.15, PDF:I.48.6, EXT:OWASP-ASVS-4.0
> **Date:** 2026-07-30

## 1. Baseline Requirements

| Requirement | Standard | Verification |
|-------------|----------|-------------|
| Authentication | JWT with refresh token rotation | Unit + integration test |
| Authorization | Role-based (RBAC), per-action check | Unit test per role |
| Input validation | FluentValidation, whitelist only | Unit test per endpoint |
| SQL injection | Parameterized queries only (EF Core) | Code review + SAST |
| XSS | Output encoding, CSP headers | SAST + browser test |
| CSRF | Anti-forgery tokens for state-changing ops | Integration test |
| Secrets | OS vault / env vars, never in source | SAST + secret scan |
| Transport | TLS 1.3 minimum, mTLS for relay | Config audit |
| Logging | No PII in logs, structured logging | Code review |
| Rate limiting | Per-IP and per-user limits | Integration test |

## 2. Verification Cadence
- SAST: every commit (CI pipeline)
- Dependency scan: daily (NuGet vulnerability check)
- Penetration test: pre-release (V20-GAT-002)
- Secret scan: every commit (pre-commit hook)

## 3. Affected Tasks
- V1-SEC-001, V1-SEC-002