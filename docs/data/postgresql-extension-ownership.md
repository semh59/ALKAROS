# PostgreSQL Extension Ownership and Rollback Policy

> **Task:** V0-DAT-007
> **Status:** Done
> **Assignee:** Antigravity-v0-dat-007
> **Work type:** decision
> **Source basis:** CORR:C52
> **Date:** 2026-08-16
> **Approver:** Semih (product owner) — 2026-08-16
> **Decision type:** Technical & Data architecture decision

## 1. Context and Problem Statement

The `btree_gist` extension in PostgreSQL 18 is required by the `catalog` module for exclusion constraints on
`catalog.product_prices` (preventing overlapping active price date ranges for the same product and price list,
PDF:III.4.4).
Previously, extension installation was implicit or ambiguously distributed across feature migrations
(`007-product-prices`). This created ambiguities regarding:

1. Extension lifecycle ownership (which module or foundation layer owns the extension lifecycle).
2. Rollback policy (whether tearing down a feature migration or rolling back to position `000` should drop
   `btree_gist`).
3. Behavior in shared, pre-provisioned, or restricted-privilege database environments where extensions cannot or should
   not be dropped.

## 2. Selected Decision

**Dedicated Foundation Migration Ownership (`012-btree-gist-ownership`)**:

1. **Ownership**: `btree_gist` is classified as a shared system-level foundation dependency. Its lifecycle is managed
   explicitly via a dedicated migration position (`012-btree-gist-ownership`).
2. **Forward Lifecycle**:
   - Migration `012-btree-gist-ownership.up.sql` executes:

     ```sql
     CREATE EXTENSION IF NOT EXISTS btree_gist;
     ```

   - This ensures idempotent initialization on fresh empty databases as well as pre-provisioned environments.
3. **Reverse / Rollback Lifecycle**:
   - Migration `012-btree-gist-ownership.down.sql` executes:

     ```sql
     DROP EXTENSION IF EXISTS btree_gist;
     ```

   - On a clean ALKAROS-managed database, rolling back position `012` safely cleans up the extension.
   - In environments where dependent database objects outside ALKAROS exist, or in managed hosting with shared extension
     pools, failing to drop due to external dependencies or pre-existing installation is classified as **declared
     pre-existing residue** and handled safely without corrupting ALKAROS migration state.
4. **Verification Query**:

   ```sql
   SELECT extname FROM pg_extension WHERE extname = 'btree_gist';
   ```

## 3. Three Initial Database States and Rollback Expectations

| Initial Database State | Forward Action | Expected State After Forward | Rollback Action | Expected State After Rollback | Residue Classification |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Fresh Empty DB** | `CREATE EXTENSION IF NOT EXISTS btree_gist;` | `btree_gist` present in `pg_extension` | `DROP EXTENSION IF EXISTS btree_gist;` | `btree_gist` absent from `pg_extension` | Zero residue (clean teardown) |
| **2. Pre-existing Extension** | `CREATE EXTENSION IF NOT EXISTS btree_gist;` (no-op) | `btree_gist` present in `pg_extension` | `DROP EXTENSION IF EXISTS btree_gist;` | `btree_gist` present if external dependency/lock exists, or dropped | Acceptable pre-existing shared residue |
| **3. ALKAROS Re-run / Rehearsal** | `CREATE EXTENSION IF NOT EXISTS btree_gist;` | `btree_gist` present in `pg_extension` | `DROP EXTENSION IF EXISTS btree_gist;` | Exact mirror of pre-migration state | Deterministic forward/reverse symmetry |

## 4. Rejected Alternatives

1. **Feature Migration Ownership (Embedded in `007-product-prices`)**:
   - *Rejected*: Embedding `CREATE EXTENSION` within a domain feature table migration tightly couples foundation
     infrastructure to a specific business table and creates rollback failure if another module later relies on
     `btree_gist`.
2. **External Provisioning Only (No Migration)**:
   - *Rejected*: Requiring manual DBA intervention before running automated integration tests or local container setups
     violates zero-configuration test automation and CI/CD requirements.
3. **`CASCADE` Dropping on Rollback**:
   - *Rejected*: Using `DROP EXTENSION btree_gist CASCADE` risks silently dropping unintended dependent domain objects.
     Standard fail-closed `DROP EXTENSION IF EXISTS btree_gist;` is enforced.

## 5. Invariants for Consumers

- `V1-FND-021` will implement the additive `012-btree-gist-ownership.up.sql` and `.down.sql` migrations matching this
  exact contract and verify forward/reverse on PostgreSQL 18.
- `V1-CAT-002` and `V1-CAT-003` can safely rely on `btree_gist` being provisioned prior to applying exclusion
  constraints without embedding extension management in catalog migrations.

## 6. Affected Tasks

- `V1-FND-021` (Integration implementation and tests)
- `V1-CAT-002` (Product pricing exclusion constraints)
- `V1-CAT-003` (Non-negative price bounds)
- `V0-GOV-036` (Decision-gate reconciliation)
