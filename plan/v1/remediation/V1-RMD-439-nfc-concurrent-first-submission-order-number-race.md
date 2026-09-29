# V1-RMD-439 - NFC'de aynı gönderimin eşzamanlı ilk iki isteğinden birinin 503 alması

- Task ID: V1-RMD-439
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

PR #11'in CI koşusunda (head 36f1dac0) `ALKAROS.Host.Experience.NfcOrdering.Tests` içindeki
`ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder` düştü: aynı NFC gönderiminin eşzamanlı iki ilk
isteğinden biri 503 ("Veritabanı işlemi tamamlanamadı") aldı. Aynı test bu değişikliklerden önceki head'de de
(ecab7f66, master birleştirmesi) yerelde 6 koşunun 5'inde düştü; yani PR'dan bağımsız, var olan bir yarış.

Kök neden (PostgreSQL günlüğü): `NfcOrderingStore.PlaceOrderAsync` aynı gönderimin daha önce kaydedilip
kaydedilmediğine masa kilidini (`FOR UPDATE`) almadan önce bakıyor. İkinci istek kilidi bekliyor, ilki commit edince
kilidi alıyor ve kendi siparişini eklemeye çalışıyor. Sipariş numarası masa numarası ve saniyenin yüzde biri
(`NFC-{masa}-{HHmmssff}`) ile üretildiği için iki istek aynı yüzde birde düşerse ekleme, yakalanan
`ux_orders_table_submission` yerine `orders_order_number_key` benzersizlik ihlaliyle düşüyor; bu yakalanmadığı için
503 dönüyor. Müşteri tarafında aynı sepet iki kez gönderildiğinde (çift dokunma, ağ tekrarı) bir istek hata görür.

Bu görev: gönderim kontrolü masa kilidi alındıktan sonra aynı transaction içinde yeniden yapılır; kilidi bekleyen
istek, ilk isteğin siparişini bulur ve aynı siparişi döner (ekleme denemesine hiç girmez).

## Owned surface

- `plan/v1/remediation/V1-RMD-439-nfc-concurrent-first-submission-order-number-race.md`
- `evidence/V1-RMD-439/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-001
  sahipliğinde) — yalnız masa kilidinden sonraki gönderim kontrolü

## In scope

- `PlaceOrderAsync` içinde kilit sonrası yeniden kontrol.

## Out of scope

- Sipariş numarası biçiminin değiştirilmesi (farklı gönderimler masa kilidiyle zaten sıraya giriyor).

## Dependencies

- V1-RMD-438

## Acceptance evidence

- `ALKAROS.Host.Experience.NfcOrdering.Tests` (gerçek PostgreSQL 18) 20 ardışık koşunun hepsinde 19/19
  (`evidence/V1-RMD-439/tests.log`). Düzeltme olmadan aynı döngüde 20 koşunun 9'unda
  `ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder` düşer (`evidence/V1-RMD-439/red-without-fix.log`).
- Semih'in elle deneyebileceği senaryo: NFC menüsünde sepeti gönder düğmesine hızlıca iki kez basın; iki istek de
  aynı siparişi gösterir, hata mesajı çıkmaz ve mutfağa tek fiş gider.

## Handoff

- None
