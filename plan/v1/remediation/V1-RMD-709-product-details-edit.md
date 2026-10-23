# V1-RMD-709 - Ürün bilgilerini düzeltme

- Task ID: V1-RMD-709
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulgusu B012'nin düzenleme kısmı (K10) ve K08 kapanır. Yönetici açılmış bir ürünün SKU, ad, kategori, KDV profili, açıklama ve sıra bilgisini düzeltebilir; yanlış girilen ürün için yeni ürün açmak gerekmez. KDV profili var olan aktif bir profil olmalıdır. Ürün güncellemesi fiyat önbelleğini artık yazmaz; böylece eşzamanlı fiyat yazımı ezilemez.

## Owned surface

- `plan/v1/remediation/V1-RMD-709-product-details-edit.md`
- `evidence/V1-RMD-709/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Catalog/** - yalnız ürün bilgisi güncelleme ucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Catalog/ProductCatalog/** - yalnız ürün bilgisi güncelleme ve fiyat önbelleği yazımının kaldırılması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/** - yalnız ürün düzenleme formu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız ürün güncelleme bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Catalog/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Catalog/ProductCatalog/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız yeni uç
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ürün bilgisi güncelleme ucu ve ekranı, KDV profili doğrulaması, fiyat önbelleği yazımının güncellemeden çıkarılması, testler.

## Out of scope

- Kategori, KDV profili, fiyat ve seçenek düzeltme; KDV oranı değişikliği PO kararı bekler.
- Ürün tipi ve stok modu değişikliği.

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-709/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
