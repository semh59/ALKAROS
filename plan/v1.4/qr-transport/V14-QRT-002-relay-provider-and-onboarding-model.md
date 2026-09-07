# V14-QRT-002 - Select relay provider and per-restaurant onboarding model

- Task ID: V14-QRT-002
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

`V0-ARC-009`'un "Public relay: Managed cloud service" seçeneğini, ALKAROS'un
merkezi bir hosting/SaaS işletmesi hâline gelmesini gerektirmeyen somut bir
provider ve restoran-başına onboarding modeliyle bağlamak.

## Owned surface

- `plan/v1.4/qr-transport/V14-QRT-002-relay-provider-and-onboarding-model.md`
- `docs/architecture/qr-relay-provider-decision.md`
- `plan/v1.4/qr-transport/V14-QRT-001-public-relay-transport.md` (yalnız
  Goal/Dependencies'e bu kararın somutlaştırdığı provider/model referansını
  eklemek için; `V14-QRT-001`'in kendi scope/Owned surface'ini değiştirmez).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Relay provider seçimi, domain sahiplik modeli, restoran onboarding
  otomasyonunun ilkesi.

## Out of scope

- Connector'ın gerçek implementasyonu (`V14-QRT-001`), setup wizard kod
  detayları, `V0-QRG-001`'in ihtiyaç duyduğu gerçek domain/credentials kaynağı.

## Dependencies

- V0-ARC-009

## Deliverables

- Tek decision record: `docs/architecture/qr-relay-provider-decision.md`.

## Acceptance evidence

- Karar, seçilen provider/modeli, reddedilen alternatifleri (merkezi SaaS
  relay, yalnız-Wi-Fi, restoran-kendi-domain'i) ve etkilenen task ID'lerini
  (`V14-QRT-001`, `V0-QRG-001`) adıyla kaydeder.
- `V0-ARC-009`'un topology kararı veya `docs/architecture/qr-relay-topology.md`
  değişmedi (before/after hash aynı).

## Handoff

- V14-QRT-001
- V0-QRG-001
