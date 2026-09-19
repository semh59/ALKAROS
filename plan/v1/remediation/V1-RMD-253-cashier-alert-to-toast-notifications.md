# V1-RMD-253 - Replace Cashier's native alert() dialogs with the approved toast pattern

- Task ID: V1-RMD-253
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`src/Clients/Cashier/wwwroot/cashier-app.js`'in `dispatchOrderToKitchen()`
akışı 7 yerde (grep ile doğrulandı) native `alert()` kullanıyor — hem hata
mesajları hem de başarı onayı için (bir siparişi mutfağa göndermenin başarılı
sonucu bile `alert('Sipariş mutfağa iletildi. ...')` ile gösteriliyor).
`foundations.md §5.3` zaten bir bildirim sözlüğü onaylamış: "Toast
(kendiliğinden kapanır) — Başarılı işlem... 3-5sn". `WaiterPwa`
(`js/toast.js`) ve `PosTerminal` (`StateMessage` primitive) bu sözlüğü
kullanıyor; Cashier hiç kullanmıyor.

Sonuç: kasiyer, kuyrukta müşteri beklerken, **her tek sipariş gönderiminde**
native tarayıcı diyaloğunu elle kapatmak zorunda kalıyor — sistemin en sık
tekrar eden işleminde gereksiz bir ekstra dokunuş. Bu görev yalnız `alert()`
çağrılarını, projenin zaten onaylı bildirim deseniyle değiştiriyor; akışın
kendisi (hangi adımda hangi mesaj gösterildiği) değişmiyor.

## Owned surface

- `evidence/V1-RMD-253/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
  (birikimli olarak birçok görevin sahipliğinde, bkz. V1-RMD-238) — 7 adet
  `alert(...)` çağrısı yeni bir `showToast(message, tone)` yardımcı
  fonksiyonuyla değiştirilir; `el` DOM önbelleğine `toastRegion` eklenir.
  Hiçbir başka davranış (hangi koşulda hangi mesajın gösterildiği,
  dispatch akışının adımları) değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
  (birikimli olarak birçok görevin sahipliğinde) — yeni bir
  `#toastRegion` konteyner elemanı eklenir, mevcut yapı değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
  (V1-CUI-008 sahipliğinde kalır; V1-RMD-252 de burada aynı desenle ek
  yapmıştı) — `.toast-region`/`.toast` stilleri eklenir, mevcut token/yapı
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/cashier-app.test.js
  (birikimli olarak birçok görevin sahipliğinde, bkz. V1-RMD-109/238) —
  `alert` bekleyen mevcut assertion'lar toast DOM'unu bekleyecek şekilde
  güncellenir; test sayısı/kapsamı değişmez.

## Dependencies

- None

## Acceptance evidence

- `tests/Clients/StaticApps` vitest paketi (güncellenmiş assertion'larla)
  yeşil kalır.
- Gerçek Chromium ile önce/sonra kanıtı: bir siparişi mutfağa gönder,
  `window.alert` hiç çağrılmadığını (`page.on('dialog', ...)` hiç
  tetiklenmediğini) ve ekranda kendiliğinden kapanan bir toast göründüğünü
  doğrula (`evidence/V1-RMD-253/`).
- `dotnet build`/`dotnet test`: bu ortamda .NET SDK kurulu değil (aynı
  V1-RMD-252'deki gibi) — Owned surface yine yalnız statik dosyalar,
  hiçbir C# değişmiyor.
- `python tools/plan-audit/plan_audit_tool.py validate` → yeni hata yok.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Not: ilk denemede toast konteyneri sağ-altta konumlandırılmıştı ve
  birincil "Siparişi Onayla & Mutfağa İlet" butonunu kısmen örtüyordu;
  gerçek ekranda fark edilip `top: 68px; right: 16px`'e taşındı (bkz.
  `evidence/V1-RMD-253/verification.md`). Küçük ikincil butonlarla
  (Beklet/Fişler/İptal) ~5sn'lik kısmi görsel kesişim kalıyor.
- Semih'in elle deneyebileceği senaryo: Cashier'da bir sipariş oluştur ve
  "Siparişi Onayla & Mutfağa İlet"e bas — hiçbir "Tamam"a basma zorunluluğu
  olmadan, ekranda birkaç saniyeliğine bir onay mesajı belirip kendiliğinden
  kaybolmalı.

## Handoff

- None
