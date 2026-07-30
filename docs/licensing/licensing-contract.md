# Offline-Safe Licensing Contract

> **Task:** V0-LIC-001
> **Status:** InProgress
> **Assignee:** codex-v0-lic-001
> **Work type:** decision
> **Source basis:** PDF:II.2.24, PDF:III.26
> **Date:** 2026-07-30

## 1. Licensing Model
- One-time activation per store (machine-bound).
- License key signed with Ed25519 (offline verification).
- Machine binding: hardware fingerprint (CPU ID + disk serial).
- Store limit: configurable per license (default: 1 store, 5 devices).

## 2. Offline Operation
- License verified locally (no internet required after activation).
- Grace period: 30 days if license check fails (clock skew tolerance).
- After grace period: read-only mode (no new orders, existing data accessible).

## 3. Rules
1. License activation: one-time, online, binds to machine fingerprint.
2. Transfer: deactivate on old machine, activate on new (requires internet).
3. Clock rollback: detected via monotonic clock + last-known-timestamp; triggers grace period.
4. Recovery: license re-issuance by vendor support (manual process).

## 4. Affected Tasks
- V20-LIC-001, V20-LIC-002