# V0-ARC-002 - Define local-first synchronization contract

- Task ID: V0-ARC-002
- Status: Blocked
- Assignee: codex-v0-arc-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.1.1
- PDF:I.4
- PDF:I.51

## Goal

Waiter PWA ve local backend arasındaki offline queue, replay, conflict ve reconnect davranışını tanımlamak.

## Owned surface

- `docs/architecture/local-first-sync-contract.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Client operation identity, ordering, clock policy, conflict rejection, response replay ve offline capability
  boundaries.

## Out of scope

- QR public relay ve provider webhook akışları.

## Dependencies

- V0-ARC-001

## Blocker

- Candidate evidence, `V0-ARC-001` `Done` olmadan kabul edilemez; ancak tam
  dependency zinciri kapatılıp acceptance yeniden doğrulanınca görev `Planned` olur.

## Deliverables

- V0-ARC-002 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Duplicate, out-of-order, stale version ve reconnect senaryolarının deterministik sonucu var.

## Handoff

- V1-ORD-002
- V1-IAM-003
