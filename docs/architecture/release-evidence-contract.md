# Release Evidence Contract

> **Task:** V0-ARC-008
> **Status:** Done
> **Assignee:** codex-v0-arc-008
> **Work type:** decision
> **Source basis:** EXT:CYCLONEDX-1.7, EXT:SLSA-1.2, CORR:C16
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Release Evidence Schema

| Field | Value |
| ------- | ------- |
| Artifact ID | Semantic version (e.g., v2.0.0) |
| Hash algorithm | SHA-256 |
| Signing | Code signing certificate (self-signed for internal, CA for public) |
| SBOM format | CycloneDX 1.7 (JSON) |
| Provenance level | SLSA Build Level 3 |
| Retention | 10 years (matching fiscal document retention) |

## 2. Evidence Package Contents

1. Artifact binary + SHA-256 checksum
2. Digital signature
3. CycloneDX SBOM (all dependencies)
4. SLSA provenance attestation
5. Build log hash
6. Test result summary

## 3. Verification

- Release rejected if: hash mismatch, signature invalid, SBOM missing, or provenance below SLSA Level 3.

## 4. Affected Tasks

- V20-REL-001, V20-GAT-002
