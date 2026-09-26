# V12-TGO-004 - Uber Eats Trendyol Go menüsünde ürün durumunu ve fiyatı yayınla

- Task ID: V12-TGO-004
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: integration
- Surface state: Planned

## Goal

Stok yayınını Trendyol Go ürün aktif/pasif durumuna, katalog yayınını fiyat güncellemesine bağlamak. Fiyat
güncellemesi kuyruğa alındığı için toplu istek sonucu izlenir. Sağlayıcı davranışı yalnız herkese açık Uber Eats Trendyol Go geliştirici belgesine dayanan doğrulanmamış taslaktır; gerçek kanıt V12-TGO-001'e aittir.

## Owned surface

- `src/Modules/OnlineOrdering/Providers/TrendyolGo/Menu/**`
- `tests/Modules/OnlineOrdering/Providers/TrendyolGo/Menu/**`
- `evidence/V12-TGO-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kanal kaydı.
  - src/Modules/OnlineOrdering/Credentials/ (V12-OUI-003) — mağaza kimliği alanı.
  - src/Clients/PosTerminal/src/features/online-platform-credentials/ (V12-OUI-003) — alan etiketi.
  - tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. `IAvailabilityChannelPublisher`: adet sıfırsa ürün pasif, değilse aktif; Trendyol Go stok adedi almaz.
2. `ICatalogChannelPublisher`: fiyat güncellemesi ve `batchRequestId` sonucunun sorgulanması; başarısız satır yayın hatası olarak kaydedilir.
3. Menü okuma ile ürün eşlemesinin (V12-ONL-008) doğrulanması.

## Out of scope

- Menüde ürün oluşturma (satıcı panelinde yapılır).

## Dependencies

- V12-TGO-002

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; mutasyon kontrolü `evidence/V12-TGO-004/` altında.
- `task_scope_tool.py --task-id V12-TGO-004 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
