# V1-RMD-274 - Fire (zayiat) kaydı yönetici uç noktasından yapılabilir

- Task ID: V1-RMD-274
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-273` borç listesinin "fire kaydı" ailesi: `V11-INV-006` `WasteRecordingService`
(bozulma, son kullanma, hazırlık hasarı, üretim firesi; stok hareketi yazıp bakiyeyi
düşer, idempotent, yetersiz stokta reddeder) yazılmış ve DI'da kayıtlıydı, çağıranı yoktu:
bir müdür fire kaydedemiyordu.

`inventory.manage` yönetici yüzeyine (V1-RMD-143 grubu) eklendi:
`POST /api/v1/management/inventory/stock-items/{id}/waste` ve
`GET /api/v1/management/inventory/waste?wasteSource=&sourceReferenceId=`.
Kaydı yapan yönetici oturumdan alınır (istemci kimlik gönderemez). Türkçe hata gövdeleri:
`INSUFFICIENT_STOCK` 409, gerekçe/miktar/birim/kaynak geçersizse 400, kalem/konum yoksa 404.

## Owned surface

- `plan/v1/remediation/V1-RMD-274-waste-recording-http-surface.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Inventory/StockMasterEndpoints.cs
  (V1-RMD-143 sahipliğinde kalır — yalnız fire kaydı servisleri, iki uç nokta ve hata eşlemeleri)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Inventory/StockMasterContracts.cs
  (aynı sahiplikte — yalnız fire sözleşmeleri)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Inventory/StockMasterHttpTests.cs
  (aynı sahiplikte — 4 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Inventory/StockMasterTestDatabase.cs
  (yalnız bakiye ve fire kaydı sayacı yardımcıları)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-272 sahipliğinde — yalnız ulaşılabilir olan 4 fire kaydı girişi silindi)

## In scope

1. İki uç nokta, hata eşlemesi, gerçek Postgres testleri, borç listesinden çıkarma.

## Out of scope

- Fire için yönetici arayüzü ekranı.
- Porsiyon rezervasyonu iptalinden otomatik fire (`V11-RSV-003`, ayrı aile).

## Dependencies

- V11-INV-006
- V1-RMD-143
- V1-RMD-272

## Acceptance evidence

- Host.Experience.Inventory (UTF8 Postgres 18): 20/20. Yeni: 10 kg'dan 2,5 kg fire → 7,5 kg, aynı
  anahtarla tekrar → aynı kayıt, bakiye bir kez düştü, kayıt sayısı 1, kaydeden yönetici oturumdan;
  rafta olandan büyük fire 409 `INSUFFICIENT_STOCK` ve bakiye/kayıt değişmedi; boş gerekçe ve geçersiz kaynak
  400, oturumsuz 401; kaynak referansına göre listeleme.
- `python tools/consistency-audit/consistency_audit.py` ve `python tools/plan-audit/plan_audit_tool.py validate` temiz.

## Handoff

- None
