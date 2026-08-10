# V20-REL-002 - Execute non-production pilot rehearsal

- Task ID: V20-REL-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54

## Goal

Immutable release candidate'ı production-equivalent fakat non-production ortamda yalnız synthetic veya yetkili sanitized
data ile pilot rehearsal olarak çalıştırmak.

## Owned surface

- `release/evidence/pilot/**`
- Bu görev release artifactını veya ürün kodunu değiştiremez.

## In scope

- Kurulum, temsili vardiya workflow, onaylı sanal alanlara/cihazlara yönelik entegrasyonlar, izleme, hata
  tetikleyicileri, geri alma karar zamanlaması ve kusur yakalama.

## Out of scope

- Gerçek müşteri verileri, gerçek payment/mali düzenleme, production dağıtımı ve kusur düzeltmeleri.

## Dependencies

- V20-REL-001
- V20-INT-001
- V20-INT-002
- V20-INT-003
- V20-INT-004
- V20-INT-005
- V20-INT-006
- V20-UAT-003

## Deliverables

- Pilot rehearsal transcript'i, operational metrics, defect register ve rollback-decision evidence.

## Acceptance evidence

- Approved workflow ve reliability threshold'ları exact release artifact üzerinde geçer; real customer, real payment
  veya real fiscal issuance kullanılmaz.
- `V20-INT-001`, `V20-INT-002` veya `V20-INT-004` kanıtlı `NotApplicable` ise ilgili provider workflow'u pilot kapsamına
  dahil edilmez; kalan approved workflow'lar exact release artifact üzerinde yine doğrulanır.
- `V20-UAT-003` kanıtlı `NotApplicable` ise recovery/exception senaryoları pilot kapsamına dahil edilmez; kalan approved
  workflow'lar exact release artifact üzerinde yine doğrulanır.
- `V20-REL-001` kanıtlı `NotApplicable` ise release adayı paketlemesi beklenmez; kalan approved workflow'lar exact
  release artifact üzerinde yine doğrulanır.

## Handoff

- V20-GAT-002
- V20-REL-003
