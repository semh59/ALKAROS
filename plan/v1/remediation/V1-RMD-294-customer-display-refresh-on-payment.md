# V1-RMD-294 - Müşteri ekranı ödeme, indirim ve iptalde güncellenir

- Task ID: V1-RMD-294
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`CustomerDisplayHub.SnapshotChanged` yalnız sipariş oluşturma, kalem ekleme/çıkarma ve gönderme uç noktalarından yayınlanıyor (`DualScreenApplication.Endpoints.cs`); ödeme (`DualScreenApplication.Payments.cs`), nakit tahsilat ve hesap indirimi sonrası yayın görünmüyor. Bu durumda müşteri ekranı ödeme sonrası eski tutarı gösterebilir. Kodda yalnız okuma ile saptandı; önce yerelde doğrulanır, doğruysa ödeme ve hesap değişikliği uç noktalarına yayın eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-294-customer-display-refresh-on-payment.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (yalnız yayın çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (yalnız yayın çağrısı)

## In scope

1. Yerelde tekrar üretme, ödeme ve nakit tahsilat sonrası yayın, testler.

## Out of scope

- Müşteri ekranı yeni tasarımı.

## Dependencies

- V1-RMD-282

## Acceptance evidence

- Host testi: ödeme ve nakit tahsilat sonrası hub grubuna `SnapshotChanged` gider (mutasyon kontrolü ile).
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
