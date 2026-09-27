# V12-ONL-011 - Restoranı platformda aç, kapat, yoğun moduna al

- Task ID: V12-ONL-011
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9); closed out by Claude Sonnet 5
- Work type: integration
- Surface state: Planned

## Source basis

- PO:2026-09-27
- EXT:YSP-PARTNER-2.0.2
- EXT:TGO-MEAL-API

## Goal

Yönetici Online Yemek ekranından her platformda restoranı açar, bugünlük kapatır ya da belirli bir süre "yoğunum"
diye sipariş almayı durdurur; ekran platformdaki güncel durumu gösterir. Sağlayıcı davranışı yalnız herkese açık
belgelere dayanan doğrulanmamış taslaktır (Yemeksepeti `YSP-PARTNER-2.0.2` Outlet Management; Trendyol Go
`TGO-MEAL-API` Restaurant Integration); gerçek kanıt `V0-YSP-001` ve `V12-TGO-001`'e aittir. `V12-GOV-009` ile açıldı.

## Owned surface

- `src/Modules/OnlineOrdering/StoreStatus/**`
- `src/Host/Experience/OnlineOrdering/OnlineStoreStatusEndpoints.cs`
- `src/Host/Experience/OnlineOrdering/OnlineStoreStatusHostedService.cs`
- `src/Clients/PosTerminal/src/features/online-store-status/**`
- `database/migrations/V12/V12-ONL-011/**`
- `evidence/V12-ONL-011/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-27 kararı):
  - src/Clients/PosTerminal/src/features/online-hub/ (V12-OUI-004) — kontrollerin yerleştirilmesi.
  - src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/ (V12-ONL-003) — Yemeksepeti restoran durumu çağrıları.
  - src/Modules/OnlineOrdering/Providers/TrendyolGo/ (V12-TGO-002) — Trendyol Go restoran durumu çağrıları.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - src/Host/DualScreen/DualScreenApplication.cs — uç nokta ve zamanlanmış servis kaydı.
  - database/MigrationComposition/order.json — migration konumu.
  - src/Host/Composition/Migrations/MigrationManifest.cs — migration konumu.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration konumu.
  - tests/Host/Experience/OnlineOrdering/ — testler ve fikstür bağlantıları.

## In scope

1. Platform başına istenen durum (açık, bugün kapalı, bir saate kadar kapalı ve nedeni) kalıcı tutulur; her
   değişiklik yapan kişiyle denetim kaydına yazılır; yalnız `integrations.manage`.
2. Yemeksepeti: `PUT /v2/chains/{chain_id}/vendors/{vendor_id}/status` (`OPEN`, `CLOSED_TODAY`, `CLOSED_UNTIL` ve
   `closed_reason`) ve `GET` ile güncel durum.
3. Trendyol Go: `PUT .../stores/{storeId}/status` (`OPEN`/`CLOSED`) ve mağaza bilgisinden `workingStatus`; süreli
   kapatmada süre dolunca açma isteği zamanlanmış servisle gönderilir.
4. Platforma iletilemeyen istek görünür kalır ve yeniden denenir; ekranda Türkçe durum ve hata.

## Out of scope

- Çalışma saatleri ve teslimat bölgesi düzenleme.

## Dependencies

- V12-OUI-004

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; PosTerminal testleri yeşil; mutasyon kontrolü
  `evidence/V12-ONL-011/` altında.
- `task_scope_tool.py --task-id V12-ONL-011 --diff-base <InProgress commit>` exit 0.
- Migration ileri ve geri boş veritabanında denenir.

## Handoff

- None
