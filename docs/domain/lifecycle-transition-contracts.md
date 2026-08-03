# Lifecycle Transition Contracts — source extraction pending approval

> **Task:** V0-DOM-001
> **Status:** Blocked
> **Assignee:** codex-v0-dom-001
> **Work type:** decision
> **Source basis:** PDF:II.5.1-II.5.15, PDF:III.6-III.23, CORR:C29
> **Access date:** 2026-08-02
> **Approver:** None — decision is not approved

## Verified boundary

PDF Part II.5 supplies the canonical state sets. The state catalogue is
reproduced in `docs/data/canonical-value-catalog.md`. It does not specify a
universal transition matrix, retry count, actor mapping, or a provider fiscal
sequence for every integration.

## Prohibited assumptions

- Payment timeout is neither an implicit approval nor an implicit decline;
  `Unknown` and `ReconciliationRequired` remain explicit source states.
- A provider/device-specific fiscal ordering, numbering or cancellation rule
  cannot be created without the relevant verified contract and approval.
- A transition, retry count or actor not supported by PDF or an approved
  decision record is not implementable.

## Revalidation exit criteria

This task can be planned only after each transition has a source or approved
decision reference, access date, named approver, rejected alternatives and
affected task identifiers. The resulting matrix must not introduce states
outside the PDF canonical sets.
