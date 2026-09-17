# V1-CUI-011 - Kasa modülü için gerçek tarayıcı (E2E) test paketi

- Task ID: V1-CUI-011
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Goal

Bağımsız bir denetim ajanı (2026-09-17, Kasa modülü kapsamlı denetimi),
`tests/E2E/`'nin YALNIZ `WaiterPwa` için gerçek bir Playwright test paketi
içerdiğini, Kasa (Cashier/PosTerminal/CustomerDisplay) için HİÇBİR gerçek
tarayıcı testi bulunmadığını doğruladı. Bu oturumda eklenen HİÇBİR özellik
(ekran koruyucu yükleme/gösterme — V1-CDP-001..004, Faz0 tasarım
migrasyonu — V1-CUI-007..010, kalan-stok rozeti — V1-WTR-054..056/
V1-CUI-010, ikram fiskal düzeltmesi — V1-RMD-228) gerçek bir tarayıcıda
"aç, tıkla, gör" şeklinde doğrulanmadı — yalnız xUnit/HTTP/vitest(jsdom)/
Python statik seviyesinde kanıt var. WaiterPwa'nın kendi E2E paketi daha
önce (V1-WTR-026/027) unit testlerin YAKALAYAMADIĞI gerçek prod bug'ları
(login sonrası tıklanamama, "Gönder" butonunun sessizce başarısız olması)
bulmuştu — aynı sınıf riskler Kasa tarafında da sessizce mevcut olabilir.

## Owned surface

- `tests/E2E/Cashier/**` (yeni — tests/E2E/WaiterPwa (V1-WTR-026) ile aynı
  yapı: `playwright.config.js`, `global-setup.js`, `specs/`, `package.json`)
- `evidence/V1-CUI-011/**`

## In scope

- Gerçek Postgres + gerçek Host binary + gerçek Chrome ile çalışan bir
  Playwright test paketi kurmak (WaiterPwa'nın kendi altyapısıyla aynı
  desen: `@playwright/test`, `pg`).
- En az şu senaryoları kapsamak:
  1. Kasiyer girişi → Kasa ekranı açılır, katalog yüklenir.
  2. Bir ürün sepete eklenir, kalan-stok rozeti doğru görünür, sipariş
     mutfağa gönderilir.
  3. Yönetici `/settings/screensaver`'dan bir görsel yükler; müşteri
     ekranı (CustomerDisplay) Idle durumuna geçtiğinde bu görseli gerçekten
     gösterir; görsel kaldırılınca varsayılan markalı karta döner.
  4. Bir ürün ikram edilir; Kasa'nın hesap görünümünde ikramın gerçek
     fiyatıyla + ayrı bir indirim satırı olarak göründüğü (sıfır/görünmez
     satır olmadığı) doğrulanır.

## Out of scope

- WaiterPwa'nın kendi E2E paketini değiştirmek.
- Ödeme/Token entegrasyonu senaryoları — henüz gerçek cihaz/sözleşme yok
  (`V0-HUG-001` Blocked), bu paket yalnız BUGÜN ÇALIŞAN Kasa özelliklerini
  kapsar.

## Dependencies

- V1-CUI-010
- V1-CDP-004
- V1-RMD-228

## Acceptance evidence

- Yeni `tests/E2E/Cashier/` paketi, gerçek Postgres + gerçek Host + gerçek
  Chrome ile çalışır (mock yok).
- Yukarıdaki 4 senaryonun tamamı yeşil geçer.
- README, WaiterPwa'nın kendi E2E README'siyle aynı çalıştırma
  talimatlarını içerir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
