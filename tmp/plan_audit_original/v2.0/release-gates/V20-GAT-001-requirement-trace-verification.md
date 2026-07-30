# V20-GAT-001 - Verify requirement trace

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Prove that every in-scope PDF requirement and every accepted audit correction has an implemented, tested or explicitly approved not-applicable disposition.

## Owned surface

- `release/evidence/requirements/**`
- Bu görev hiçbir ürün modülünün uygulama kodunu değiştiremez.

## In scope

- PDF section-to-task-to-test-to-artifact matrix, unresolved-item detection and evidence-link validation.

## Out of scope

- Implementing missing behavior, changing scope and granting legal or production approval.

## Dependencies

- V1.5 exit gate

## Deliverables

- Immutable requirement trace report for the release candidate.
- Machine-readable list of missing, failed and not-applicable rows.

## Acceptance evidence

- The verification command reports zero unowned, evidence-free or unresolved in-scope PDF rows; every not-applicable row carries named approval evidence.

## Handoff

- V20-GAT-002 and any owner of a detected gap.
