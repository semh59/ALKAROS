# V12-OUI-005 - Online Yemek Menü sekmesi ve ürün eşleme

- Task ID: V12-OUI-005
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Goal

Yönetici her platform için katalog ürünlerini platform ürünleriyle eşler, ürünün satışta olup olmadığını ve platform
fiyatı ile katalog fiyatını yan yana görür, menüyü yayınlar ve son yayının sonucunu okur. Trendyol Go'da platform
menüsü okunduğu için eşleme listeden seçilir; Yemeksepeti'nde platform ürün kodu elle girilir. Trendyol Go
eşlemesi olmadan kullanılamadığı için bu sekme onu kullanılabilir yapar. `V12-GOV-009` ile açıldı.

## Owned surface

- `src/Clients/PosTerminal/src/features/online-menu/**`
- `src/Host/Experience/OnlineOrdering/OnlineMenuEndpoints.cs`
- `evidence/V12-OUI-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-27 kararı):
  - src/Clients/PosTerminal/src/features/online-hub/ (V12-OUI-004) — sekmenin yerleştirilmesi.
  - src/Clients/PosTerminal/src/routes/ — Menü yolunun yalnız yöneticiye açılması.
  - src/Modules/OnlineOrdering/Yemeksepeti/ProductMapping/ (V12-MAP-001) — platform bazlı eşleme listesi ve kapatma.
  - src/Modules/OnlineOrdering/Providers/TrendyolGo/Menu/ (V12-TGO-004) — platform menüsünün adlarıyla okunması.
  - src/Modules/OnlineOrdering/CatalogPublishing/ (V12-ONL-004) — yayın geçmişinin okunması.
  - src/Modules/OnlineOrdering/OnlineOrderingModule.cs (V12-MAP-001) — kayıt.
  - src/Host/DualScreen/DualScreenApplication.cs — uç nokta kaydı.
  - tests/Host/Experience/OnlineOrdering/ — testler.

## In scope

1. Platform bazlı eşleme uç noktaları (`integrations.manage`): listele, eşle, eşlemeyi kapat; eşleme kuralları
   (bir platform ürünü tek katalog ürünü, bir katalog ürünü platform başına tek kod) mevcut servistedir.
2. Trendyol Go platform menüsünün ürün adı ve durumuyla listelenmesi; eşlenmemiş platform ürünleri ve menüde
   bulunmayan eşlemeler işaretlenir.
3. Menüyü yayınla düğmesi (mevcut yayın uç noktası) ve son yayınların durumu, doğrulama hataları ve son hatası;
   platform hata metni ekrana ham basılmaz.
4. Ürün başına satış durumu (stok yayınından) ve fiyat karşılaştırması.

## Out of scope

- Platformda ürün oluşturma veya adlandırma (satıcı panelinde yapılır).

## Dependencies

- V12-OUI-004

## Acceptance evidence

- İlgili test projeleri gerçek Postgres 18 üzerinde yeşil; PosTerminal testleri yeşil; mutasyon kontrolü
  `evidence/V12-OUI-005/` altında.
- `task_scope_tool.py --task-id V12-OUI-005 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
