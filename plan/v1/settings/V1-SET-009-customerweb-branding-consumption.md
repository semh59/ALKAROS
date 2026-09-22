# V1-SET-009 - CustomerWeb consumes the business's own name/color/logo

- Task ID: V1-SET-009
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-SET-007`'nin ertelediği üçüncü ve son madde: `GET /api/v1/qr/branding`
(V1-SET-007) ve `GET /api/v1/qr/logo` (V1-SET-008) artık gerçek, çağrılabilir
uç noktalar — ama CustomerWeb'in üç sayfası (Menu, OrderEntry, Bill) bunları
hiç çağırmıyordu, sayfa hâlâ sabit ALKAROS marka rengiyle (`#B5772F`) ve
isimsiz görünüyordu. Bu görev, üç sayfaya da aynı küçük, bağımsız
`loadBranding()` fonksiyonunu ekliyor (V1-WTR-018'in "bilinçli olarak ayrı
bir modül değil, kopyalanmış" kararıyla aynı desen): işletme adı ve logosu
varsa gösterilir, `--cw-accent` CSS değişkeni seçilen palet rengine ayarlanır.
Ayarlanmamışsa (varsayılan kurulum) hiçbir şey görünür değişmez — başlık
bloğu tamamen gizli kalır, aksan rengi sabit varsayılanında kalır.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Menu/wwwroot/index.html,
  menu-app.css, menu-app.js; tests/Apps/CustomerWeb/Menu/test_customer_web_menu.py
  (V12-CWB-001 sahipliğinde) — `businessBrand`/`businessLogo`/`businessName`
  başlık bloğu, `loadBranding()`.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.html,
  order-entry.css, order-entry.js; tests/Apps/CustomerWeb/OrderEntry/test_customer_web_order_entry.py
  (V12-CWB-002 sahipliğinde) — aynı desen.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.html,
  bill.css, bill.js; tests/Apps/CustomerWeb/Bill/test_customer_web_bill.py
  (V1-WTR-018 sahipliğinde) — aynı desen.
- `evidence/V1-SET-009/**` (yeni)

## In scope

1. Üç sayfanın her birine: `businessBrand` (varsayılan `hidden`) kapsayıcı,
   içinde `businessLogo` (`<img>`, varsayılan `hidden`) ve `businessName`
   (`<p>`, varsayılan `hidden`).
2. `loadBranding()` — `GET /api/v1/qr/branding`'i sayfa yüklenince,
   oturum/sepet/adisyon akışından bağımsız (fire-and-forget) çağırır:
   - `document.documentElement.style.setProperty("--cw-accent", ...)`.
   - `businessName` doluysa gösterir.
   - `hasLogo` `true`'ysa `businessLogo.src = "/api/v1/qr/logo"` yapıp gösterir.
   - İkisi de yoksa kapsayıcı gizli kalır.
   - Ağ hatası/bozuk yanıt sessizce yutulur — kozmetik bir özellik, sayfanın
     asıl işini (menü/sepet/adisyon) asla engellemez.

## Out of scope

- PosTerminal'de bir yönetici ayar ekranı — V1-SET-007/008'in kendi
  ertelemesi, ayrı bir görev.
- `--cw-accent-contrast`'ın değiştirilmesi — palet zaten her rengi beyaz
  metinle ≥4.5:1 doğruladığı için (V1-SET-007) sabit beyaz kalıyor.

## Dependencies

- V1-SET-007
- V1-SET-008

## Acceptance evidence

- `python -m pytest tests/Apps/CustomerWeb -q` → 16/16 (13 önceki + 3 yeni:
  her sayfa için başlık/CSS/JS'te `businessBrand`/`loadBranding`/
  `/api/v1/qr/branding`/`/api/v1/qr/logo`/`--cw-accent` doğrulaması).
- Gerçek Chromium (Playwright) ile stub sunucu üzerinden üç sayfanın da
  gerçek ekran görüntüsü: işletme adı ("Sahil Cafe"), gerçek bir PNG logo
  ve navi mavi (#1B4D7B) aksan rengi (tab, sepet çubuğu, gönder butonu,
  "Adisyonum" bağlantısı) uçtan uca doğrulandı
  (`evidence/V1-SET-009/{menu,order-entry,bill}-branded.png`). Ayrıca
  ayarlanmamış durumun (boş ad, `hasLogo:false`, varsayılan amber `#9C6323`)
  başlık bloğunu tamamen gizli bıraktığı ve görünümün bu görevden önceki
  hâliyle birebir aynı kaldığı ayrı bir ekran görüntüsüyle doğrulandı
  (`evidence/V1-SET-009/menu-unbranded.png`).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: `settings.manage` izniyle
  `business.name`/`business.accent_theme` ayarla ve
  `PUT /api/v1/management/business-identity/logo`'ya bir PNG yükle; QR
  kodunu okutup Menu/Sepetim/Adisyonum sayfalarının üçünün de işletmenin
  adını, logosunu ve seçilen rengini gösterdiğini gör.

## Handoff

- None
