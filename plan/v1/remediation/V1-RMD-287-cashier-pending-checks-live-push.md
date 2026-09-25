# V1-RMD-287 - Garsondan kasaya gönderilen hesap kasaya anında düşer

- Task ID: V1-RMD-287
- Status: Done
- Assignee: Claude Sonnet 5
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
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs
  (yalnız `PendingChecksChanged` sabiti ve payload kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/WaiterNotifications/WaiterNotificationsExperience.cs
  (yalnız DI kaydının değiştirilmesi)
- Yeni dosya: src/Host/Experience/Orders/CashierQueueAnnouncer.cs
- Yeni dosya: src/Host/Experience/WaiterNotifications/SignalRCashierQueueAnnouncer.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/14-till-queue-of-sent-checks.spec.js
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

- Host testi (UTF8 Postgres 18): `TableDraft` paketi 17/17. Gönderme olayı yayınlar, geri çağırma yayınlar, tekrarlanan gönderim ve reddedilen geri çağırma hiçbir şey yayınlamaz. `Composition` 10/10, `WaiterNotifications` 11/11.
- PosTerminal vitest: 7/7 (`PendingChecksWorkspace`). Hub olayı ve yeniden bağlanma listeyi anında yeniler.
- Cashier E2E (gerçek Host + Chromium): 34/34 (33 mevcut + 1 yeni). Yeni: garson hesabı gönderince kasa ekranı sayfa yenilenmeden, 2 sn içinde günceller (yedek sorgu 60 sn'ye çıkarıldı, olayın kendisi kanıtlanıyor).
- Mutasyon kontrolü: sunucu ve istemci değişiklikleri geri alınınca yeni Host testi (1/17), PosTerminal testi (1/7 senaryo etkisi doğrulandı ayrı çalıştırmayla) ve yeni E2E senaryosu kırıldı; diğer testler etkilenmedi.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.
- Kapsam notu: olay `Clients.All` ile herkese yayınlanıyor (mevcut hub deseni); hedeflenmiş teslim `V1-RMD-289`'un kapsamında.

## Handoff

- None
