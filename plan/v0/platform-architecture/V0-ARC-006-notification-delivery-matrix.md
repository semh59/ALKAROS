# V0-ARC-006 - Define notification delivery matrix

- Task ID: V0-ARC-006
- Status: Done
- Assignee: codex-v0-arc-006
- Work type: decision
- Surface state: Done

## Source basis

- PDF:I.40
- PDF:II.2.25
- CORR:C14

## Goal

Her alert severity ve event sınıfı için onaylı transport, recipient, escalation, retry ve redaction matrisini
belirlemek.

## Owned surface

- `docs/architecture/notification-delivery-matrix.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Enabled transport, recipient role, quiet hours, retry, duplicate suppression ve delivery audit.

## Out of scope

- Transport adapter code, commercial provider seçimi ve alert generation.

## Dependencies

- V0-ARC-004
- V0-ARC-005

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen delivery matrix ve disabled transport listesi.

## Acceptance evidence

- Her desteklenen alert sınıfı tek delivery/escalation yolu veya açık disabled sonucu taşır.

## Handoff

- V15-NOT-001
