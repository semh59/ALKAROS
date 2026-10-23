# V1-RMD-707 - Kasa araması ve garson menü hatası

- Task ID: V1-RMD-707
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulguları U-007/F-011 ve U-005, U-006 kapanır. Kasa ürün araması Türkçe küçük harf kuralıyla yapılır ("İskender" bulunur), filtre sonucu boşsa kasada mesaj gösterilir. Garson menüsü ilk yüklemede alınamazsa "Menü alınamadı" mesajı ve "Yeniden dene" düğmesi gösterir; menü gerçekten boşsa farklı mesaj verir.

## Owned surface

- `plan/v1/remediation/V1-RMD-707-cashier-waiter-catalog-search-and-retry.md`
- `evidence/V1-RMD-707/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/Cashier/Frontend/** - yalnız yeni arama testi
- `tests/Clients/WaiterPwa/Frontend/test_waiter_catalog_retry.py`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js - yalnız ürün arama ve boş ızgara mesajı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js - yalnız katalog hatası ve yeniden dene
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/screens/menu.js - yalnız boş menü mesajları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/state.js - yalnız katalog hata bayrağı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kasa aramasında tr-TR küçük harf, kasa boş ızgara mesajı, garson menü hatası ve yeniden dene, testler.

## Out of scope

- Garson 15 dakikalık yenileme aralığı ve üç istemcinin ortak boş mesajı; SKU otomatik ekleme (ayrı iş).

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-707/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
