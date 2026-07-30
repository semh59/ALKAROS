# V20-INS-001 - Build and verify fresh installation package

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Install the signed release candidate on a clean supported target using a deterministic, documented package.

## Owned surface

- `installer/**`, `tools/release/install/**`, `tests/Installer/FreshInstall/**`
- Bu görev uygulama modüllerinin iş mantığını değiştiremez.

## In scope

- Prerequisite checks, package signature/hash, service/database bootstrap, least-privilege identity, configuration validation and uninstall boundary.

## Out of scope

- In-place update, licensing policy and production deployment.

## Dependencies

- V15-SEC-001, V1-SET-001

## Deliverables

- Signed installation artifact and clean-machine test automation.
- Supported target/prerequisite matrix and failure diagnostics.

## Acceptance evidence

- A clean supported target installs non-interactively, verifies artifact identity, starts healthy with no embedded secret and fails safely on unmet prerequisites.

## Handoff

- V20-INS-002, V20-MIG-001 and V20-DRL-001.
