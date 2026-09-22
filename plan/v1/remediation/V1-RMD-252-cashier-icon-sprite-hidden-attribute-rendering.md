# V1-RMD-252 - Fix Cashier icon-sprite hidden attribute not rendering as hidden

- Task ID: V1-RMD-252
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bu oturumda gerçek Chromium (Playwright, `/opt/pw-browsers/chromium-1194`) ile
`src/Clients/Cashier/wwwroot/index.html`'i gerçekten çalıştırıp (API'ler
temsili örnek veriyle beslenerek) ekran görüntüsü alınırken bulundu: sayfanın
en üstünde, başlık çubuğundan önce ~150px boş beyaz bir şerit gerçekten
render ediliyor. Kök neden doğrulandı (`page.evaluate` ile bounding box
ölçümü): `index.html`'in en üstündeki ikon sprite'ı
(`<svg xmlns="..." hidden aria-hidden="true">`, satır 17) tarayıcıda
`display:block` olarak render ediliyor — çıplak `hidden` özniteliği bu
satır-içi kök `<svg>` üzerinde etkisiz kalıyor (Chromium'un `[hidden]` varsayılan
UA kuralı bu köke uygulanmıyor) ve `cashier-app.css`'te bunu telafi edecek
genel bir `[hidden] { display: none; }` kuralı yok — yalnızca
`.modal-overlay[hidden]` (satır 522) var, sprite'ı kapsamıyor.

`src/Clients/WaiterPwa/wwwroot/index.html` aynı sprite desenini kullanıyor
(`grep` ile doğrulandı) ama `waiter-app.css:100`'de genel bir
`[hidden] { display: none; }` kuralı zaten var — WaiterPwa'da bu hata
oluşmuyor. Bu görev, WaiterPwa'nın zaten sahip olduğu aynı savunmayı
Cashier'a taşır.

Sonuç: her gerçek kasiyer oturumunda sayfa yüklenince başlık çubuğu 150px
aşağıda başlıyor, ekranın kullanılabilir dikey alanı gereksiz yere azalıyor.

## Owned surface

- `evidence/V1-RMD-252/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css (V1-CUI-008 sahipliğinde kalır) — yalnızca genel bir [hidden]{display:none !important;} kuralı eklenir, mevcut token/yapı değişmez.

## Dependencies

- None

## Acceptance evidence

- Gerçek Chromium ile önce/sonra ekran görüntüsü: düzeltmeden önce başlık
  çubuğunun `top` değeri 150px, düzeltmeden sonra 0px (aynı Playwright script'i
  ile `getBoundingClientRect()` ölçümü, `evidence/V1-RMD-252/`).
- `tests/Clients/StaticApps` vitest paketi (`cashier-app.test.js` dahil)
  değişiklik öncesi ve sonrası yeşil kalır (24/24) — bu değişiklik JS
  davranışına dokunmuyor, yalnız CSS ekliyor.
- `dotnet build`/`dotnet test`: Bu ortamda (bu Claude Code oturumu) .NET SDK
  kurulu değil (`dotnet: command not found`) — çalıştırılamadı. Owned surface
  yalnız statik bir `.css` dosyası olduğundan (hiçbir `.csproj` içeriğine
  dahil değil, hiçbir C# kodu değişmiyor) gerçek risk düşük değerlendirildi,
  ama bu iddia bir derleme çıktısıyla doğrulanmadı — .NET SDK olan bir ortamda
  `dotnet build ALKAROS.slnx` ile ayrıca doğrulanmalı.
- Semih'in elle deneyebileceği senaryo: Cashier'ı aç, sayfa yüklenir yüklenmez
  başlık çubuğunun (ALKAROS logosu + oturum bilgisi) pencerenin en üstünde,
  boşluksuz başladığını gözle doğrula.
- `python tools/plan-audit/plan_audit_tool.py validate` → bu görev eklendikten
  sonra 1 hata (`C54_APPLICATION_ADMISSION_V3_FINAL_MISSING`) — bu hata bu
  görevden ÖNCE de aynı şekilde vardı (değişiklik öncesi baseline'da da
  doğrulandı), bu görevle ilgisiz, önceden var olan bir bulgu.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`.

## Handoff

- None
