# V1-RMD-702 - Ürün KDV profili olmadan açılamaz

- Task ID: V1-RMD-702
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulguları B001 (Kritik), B009 (kategori ve KDV alanı) kapanır. Yeni ürün, var olan ve aktif bir KDV profili olmadan sunucuda reddedilir (400); ürün formu zorunlu KDV profili seçicisi ve isteğe bağlı kategori seçicisi sunar. Böylece ekrandan açılan ürünler siparişe %0 KDV ile yazılamaz. Mevcut profilsiz ürünlerin düzeltilmesi ve satış yolunda reddi varsayılan oran kararına (PO sorusu S1) bağlıdır ve bu görevde değildir.

## Owned surface

- `plan/v1/remediation/V1-RMD-702-product-requires-tax-profile.md`
- `evidence/V1-RMD-702/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Catalog/** - yalnız ürün oluşturmada KDV profili doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/** - yalnız ürün formu KDV ve kategori seçicisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Catalog/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sunucuda ürün oluştururken profil var mı ve aktif mi doğrulaması, istemcide seçici ve doğrulama, testler.

## Out of scope

- Mevcut profilsiz ürünlerin dönüştürülmesi ve sipariş yollarında profilsiz ürünün reddi (S1 kararı gerekir).
- KDV profili güncelleme ve geçerlilik tarihi (S2 kararı gerekir).

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-702/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
