# V1-RMD-255 — Verification transcript

## Ortam

- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`, Playwright 1.56.1)
- Cashier statik dosyaları gerçek olarak sunuldu.

## Değişiklik

- `src/Clients/Cashier/wwwroot/brand/alkaros-logo-on-dark.png` — WaiterPwa'nın
  aynı dosyasının birebir kopyası (1005×233 PNG).
- `index.html`: `.pos-logo` içindeki `#ico-brand` ikonu + "ALKAROS" metni,
  `<img class="brand-mark" src="./brand/alkaros-logo-on-dark.png" alt="ALKAROS">`
  ile değiştirildi; artık kullanılmayan `#ico-brand` sprite tanımı kaldırıldı.
- `cashier-app.css`: WaiterPwa'nın `.brand-mark` kuralıyla birebir aynı
  (`height:24px; width:auto; display:block;`) eklendi; `.pos-logo`'nun artık
  anlamsız kalan yazı tipi/renk kuralları temizlendi (yalnızca `display:flex;
  align-items:center;` kaldı, resmi ortalamak için).

## Gerçek tarayıcı doğrulaması

```text
logo: {"naturalWidth":1005,"naturalHeight":233,"complete":true,"alt":"ALKAROS"}
```

Resim gerçekten yüklendi (`complete: true`, kırık görsel yok). `after.png`
— başlıkta artık WaiterPwa ile birebir aynı gerçek ALKAROS logosu var.

## İlgisiz, önceden var olan bir konsol hatası (bu görevle değiştirilmedi)

Sayfa yüklenirken bir 404 konsol hatası var — kaynağı `manifest.json`'ın
referans verdiği `./icon-192.png`/`./icon-512.png` (Cashier'ın wwwroot'unda
bu dosyalar hiç yok; WaiterPwa'da var). Bu hata bu görevden ÖNCE de
vardı (bu oturumun en ilk Cashier ekran görüntüsü denemesinde de aynı
mesaj görüldü) — PWA kurulum ikonu eksikliği, başlıktaki logodan ayrı bir
konu. Bilgi için not edildi, bu görevin kapsamında değiştirilmedi.

## Otomatik testler

```text
$ cd tests/Clients/StaticApps && npx vitest run cashier-app.test.js
 Test Files  1 passed (1)
      Tests  12 passed (12)
```

## Proje-geneli kontroller

- `python tools/plan-audit/plan_audit_tool.py validate` → 1 hata
  (`C54_APPLICATION_ADMISSION_V3_FINAL_MISSING`), önceden de vardı, ilgisiz.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`.

## Çalıştırılamayan kontrol

`dotnet build`/`dotnet test`: bu ortamda .NET SDK kurulu değil (aynı
V1-RMD-252/253/254'teki gibi). Owned surface yalnızca statik dosyalar.

## Kapsam dışı bırakılanlar (ayrı görev/karar gerektirir)

- **PosTerminal**: React/Vite derleme hattı farklı; asset import deseni
  ayrıca araştırılmalı — ayrı bir Task ID.
- **CustomerWeb**: müşteriye açık ekranlarda ALKAROS markasının
  gösterilip gösterilmeyeceği bir ürün kararı (Semih'in kararı gerekir),
  bu görevin "kopyala-yapıştır" kapsamına sokulmadı.
