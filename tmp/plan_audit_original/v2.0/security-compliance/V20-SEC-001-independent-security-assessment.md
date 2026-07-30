# V20-SEC-001 - Perform independent security assessment

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Independently assess release-candidate authentication, authorization, public endpoints, secrets and sensitive-data controls.

## Owned surface

- `release/evidence/security/**`
- Bu görev ürün kodunu değiştiremez; bulgular ayrı düzeltme görevine döner.

## In scope

- Assignee independence check: the assignee cannot be an implementer of the assessed controls.
- Threat-model verification, SAST/dependency/config scans, authorization abuse cases, public endpoint tests, secret scan and finding severity.

## Out of scope

- Fix implementation, legal sign-off and availability/load certification.

## Dependencies

- V15-SEC-001, V15-SEC-002, V15-SEC-003, V14-QRS-002

## Deliverables

- Reproducible assessment report, raw tool outputs and finding register.

## Acceptance evidence

- No open critical/high finding remains; every lower finding has an owner, disposition and evidence-backed severity.

## Handoff

- V20-GAT-002 and owners of security findings.
