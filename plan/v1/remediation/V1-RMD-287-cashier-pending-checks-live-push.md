# V1-RMD-287 - Garsondan kasaya gönderilen hesap kasaya anında düşer

- Task ID: V1-RMD-287
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

Kasa 'Bekleyen hesaplar' ekranı (`V1-RMD-280`) her 10 saniyede bir sorgu yapıyor; garson hesabı gönderir göndermez kasiyer en fazla 10 sn görmez. Gönderme ve geri çağırma uç noktaları kasa terminaline bir `PendingChecksChanged` olayı yayınlar; ekran olayı alınca listeyi hemen yeniler. Sorgu yedek olarak (daha seyrek) kalır; bağlantı koparsa `V1-RMD-286` ile aynı yeniden bağlanma ve telafi kuralı geçerlidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-287-cashier-pending-checks-live-push.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs
  (yalnız gönderme/geri çağırma sonrası olay yayını)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx

## In scope

1. Olay sözleşmesi ve yayın, ekranın olayla yenilenmesi, yedek sorgu aralığının uzatılması, testler.

## Out of scope

- Başka ekranların canlı güncellemesi.

## Dependencies

- V1-RMD-280
- V1-RMD-281
- V1-RMD-286

## Acceptance evidence

- Host testi: gönderme ve geri çağırma olay yayınlar; PosTerminal vitest: olay listeyi yeniler; Cashier E2E: garson gönderince kasa ekranı 2 sn içinde güncellenir.
- `plan_audit_tool.py validate` temiz.

## Handoff

- None
