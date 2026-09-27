# V1-RMD-349 - Kasa PWA manifest'inin ikon referansları artık gerçek dosyalara işaret ediyor

- Task ID: V1-RMD-349
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: "PWA manifest'inde olmayan ikon
referansı." Doğrulandı: `src/Clients/Cashier/wwwroot/manifest.json` `./icon-192.png` ve `./icon-512.png`'yi
listeliyordu, ama `src/Clients/Cashier/wwwroot/` altında BU DOSYALARIN HİÇBİRİ yoktu (yalnızca
`brand/alkaros-logo-on-dark.png`, dikdörtgen bir yatay logo, kare bir uygulama ikonu için uygun değil). Bir
tarayıcının "Ana ekrana ekle" istemi bu yüzden kırık/eksik bir ikon gösterirdi; Chrome, bir manifest'i kurulabilir
saymak için en az bir gerçek ≥192px ikon gerektirir.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/icon-192.png
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/icon-512.png
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/19-pwa-manifest.spec.js
- `plan/v1/remediation/V1-RMD-349-cashier-pwa-manifest-icons.md`

## In scope

1. `WaiterPwa`'nın kendi gerçek, amaca uygun kare ALKAROS uygulama ikonları (`icon-192.png`/`icon-512.png` —
   192×192 ve 512×512, "AR" logomarkı) `Cashier`'in `wwwroot`'una kopyalandı. Bu YENİ marka sanatı icat etmiyor
   — aynı repodaki kardeş PWA'nın (aynı ALKAROS ürün ailesi) zaten var olan, gerçek uygulama ikonunu yeniden
   kullanıyor; Cashier'in kendi geniş yatay banner logosunu (`brand/alkaros-logo-on-dark.png`) kareye
   sıkıştırmaya çalışmak yerine.
2. Gerçek bir Playwright E2E testi (`19-pwa-manifest.spec.js`): bu paketin zaten servis ettiği GERÇEK inşa
   edilmiş Kasa kabuğuna karşı `manifest.json`'ı gerçek bir tarayıcı gibi çekiyor, listelediği HER ikonun
   gerçekten `200`/`image/png` döndürdüğünü ve boş olmadığını doğruluyor.

## Out of scope

1. `src/Clients/WaiterPwa/wwwroot/manifest.json` — kendi ikonları zaten mevcut ve doğru, bu bulgu Kasa'ya
   özgüydü.
2. `src/Clients/PosTerminal/dist/cashier/manifest.json` — bu, `.gitignore`'da olan bir derleme çıktısı
   (`dist/`), kaynak değil; bir sonraki `pnpm build` + suite'in kendi `global-setup.js`'i tarafından
   `src/Clients/Cashier/wwwroot/`'tan otomatik olarak yeniden kopyalanıyor.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` (specs/19-pwa-manifest.spec.js): 1/1 test geçti — GERÇEK Chromium + gerçek Host'un
  servis ettiği GERÇEK inşa edilmiş Kasa kabuğuna karşı, `manifest.json`'ı çekip listelediği iki ikonun da
  gerçekten `200 image/png` döndürdüğü doğrulandı.
- Mutasyon kontrolü: hem kaynak (`src/Clients/Cashier/wwwroot/icon-*.png`) hem inşa edilmiş çıktı
  (`dist/cashier/icon-*.png`) dosyaları geçici olarak kaldırıldı, test GERÇEK bir `404` ile kırmızıya döndü
  (`./icon-192.png -> 404`) — denetimin bulgusunu birebir doğruluyor. Dosyalar geri yüklendi, paket yeniden
  1/1 yeşile döndü.

## Handoff

- None
