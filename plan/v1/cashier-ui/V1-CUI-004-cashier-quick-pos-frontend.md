# V1-CUI-004 - Cashier quick POS frontend implementation

- Task ID: V1-CUI-004
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Goal

Kasiyerlerin tezgah üstü hızlı satış, doğrudan ürün/barkod okutma, adisyon park etme/geri çağırma, anlık nakit tahsilat ve para üstü hesaplama işlemlerini gerçekleştirebileceği yüksek performanslı, dokunmatik ve klavye/barkod uyumlu Kasiyer Hızlı Satış Arayüzünü (Cashier Quick POS Frontend) geliştirmek.

## Owned surface

- `plan/v1/cashier-ui/V1-CUI-004-cashier-quick-pos-frontend.md`
- `src/Clients/Cashier/wwwroot/**`
- `tests/Clients/Cashier/Frontend/**`
- `evidence/V1-CUI-004/**`

## In scope

- Kasiyer masaüstü ve tezgah terminaline özel yüksek yoğunluklu, hızlı erişimli web/PWA arayüzü (`index.html`, `manifest.json`, `cashier-app.css`, `cashier-app.js`).
- Hızlı ürün paneli, kategori sekmeleri, barkod/ürün arama ve tek dokunuşla sepete ekleme.
- Aktif fiş/adisyon yönetimi: Kalem miktarı, indirim, ikram, silme ve satır notu.
- Hızlı Nakit Tahsilat ve Para Üstü Motoru (Hızlı banknot tuşları: ₺50, ₺100, ₺200, ₺500, Tam Tutar).
- Fiş bekletme (Park) ve bekleyen fişleri geri çağırma (Recall) mekanizması.
- Kasa oturumu, aktif vardiya ve kasa açma/kapama durum paneli.
- Frontend mimari ve birim testleri.

## Out of scope

- Host API veya veritabanı şemasında geriye dönük uyumsuz değişiklik yapmak.
- Harici EFT-POS cihazı yazılım protokolü kodlamak.

## Dependencies

- V1-CUI-003
- V1-RMD-036

## Acceptance evidence

- Kasiyer arayüzü statik varlıkları ve PWA manifesti eksiksiz yüklenir.
- Hızlı ürün ekleme, sepet güncelleme, hızlı nakit ödeme ve para üstü hesabı hatasız çalışır.
- Fiş bekletme (Park) ve geri çağırma (Recall) akışları doğrulanır.
- Arayüz testleri ve plan audit doğrulayıcısı exit code `0` verir.

## Handoff

- V20-UAT-001
