# V1-RMD-252 — Verification transcript

## Ortam

- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`, Playwright 1.56.1)
- Cashier statik dosyaları gerçek olarak sunuldu (`src/Clients/Cashier/wwwroot`),
  `/api/v1/*` uç noktaları temsili örnek veri döndüren yerel bir stub sunucuyla
  yanıtlandı (canlı backend bu ortamda kurulu değil).

## Ölçüm — düzeltmeden önce

```text
[before] {"headerTop":150,"headerHeight":52,"svgDisplay":"block"}
```

`before.png`: başlık çubuğu (ALKAROS logosu + oturum bilgisi) pencerenin
150px aşağısında başlıyor; üstünde boş beyaz bir şerit var.

## Düzeltme

`src/Clients/Cashier/wwwroot/cashier-app.css`'e (body kuralından hemen sonra)
eklendi:

```css
[hidden] {
  display: none !important;
}
```

## Ölçüm — düzeltmeden sonra

```text
[after] {"headerTop":0,"headerHeight":52,"svgDisplay":"none"}
```

`after.png`: başlık çubuğu pencerenin en üstünde, boşluksuz.

## Yan etki taraması

`index.html`'deki her `hidden` özniteliği taraması (3 sonuç):

1. `<svg ... hidden>` (satır 19) — bu görevin konusu, artık `display:none`.
2. `#dispatchHint` (satır 118) — sıradan bir `<div>`; tarayıcının varsayılan
   `[hidden]` UA kuralı zaten çalışıyordu, yeni kural aynı sonucu üretmeye
   devam ediyor (davranış değişmedi).
3. `#parkedModal` (satır 127) — zaten `.modal-overlay[hidden]` kuralıyla
   `display:none` idi; yeni genel kural aynı sonucu üretiyor (davranış
   değişmedi, çakışma yok — ikisi de `display:none`).

## Otomatik testler

```text
$ cd tests/Clients/StaticApps && npx vitest run
 Test Files  3 passed (3)
      Tests  24 passed (24)
```

Değişiklik öncesi ve sonrası birebir aynı sonuç (24/24) — bu değişiklik
yalnızca CSS ekliyor, `cashier-app.js` davranışına dokunmuyor.

## Çalıştırılamayan kontrol

`dotnet build` / `dotnet test`: bu Claude Code oturumunda .NET SDK kurulu
değil (`dotnet: command not found`). Owned surface yalnızca statik bir
`.css` dosyası (hiçbir `.csproj`'a dahil değil, hiçbir C# değişmedi) —
gerçek risk düşük değerlendirildi, ama bu iddia bir gerçek derleme
çıktısıyla burada doğrulanamadı.
