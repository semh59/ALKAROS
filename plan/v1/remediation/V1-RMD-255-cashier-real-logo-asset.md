# V1-RMD-255 - Give Cashier the real ALKAROS logo instead of a generic icon+text mark

- Task ID: V1-RMD-255
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Doğrulandı (`find`/`grep`, 2026-09-19 rakip kıyaslama turunda): dört
istemciden (Cashier, WaiterPwa, PosTerminal, CustomerWeb) **yalnızca
WaiterPwa** gerçek bir logo dosyasına sahip
(`src/Clients/WaiterPwa/wwwroot/brand/alkaros-logo-on-dark.png`, geçerli
1005×233 PNG). Cashier'ın başlığı bunun yerine genel bir ikon sprite'ından
(`#ico-brand`, PosTerminal'in Icon.tsx'inden mirror'lanmış bir ok/şimşek
şekli) + düz "ALKAROS" metninden oluşuyor.

Bu görev yalnız Cashier'ı kapsıyor — WaiterPwa ile aynı iç/personel
kullanımlı bir istemci, aynı koyu (`--color-ink`) başlık zeminini kullanıyor
(logo dosyası zaten "on-dark" varyantı, renk uyarlaması gerekmiyor),
aynı `brand-mark` deseniyle bire bir eşleşiyor. PosTerminal (React,
farklı asset/derleme hattı) ve CustomerWeb (müşteriye açık ekran; ALKAROS
markasının müşteriye gösterilip gösterilmeyeceği bir ürün kararı, bu
görevin kapsamı dışında — ayrı ele alınmalı) bilerek dışarıda bırakıldı.

## Owned surface

- `src/Clients/Cashier/wwwroot/brand/alkaros-logo-on-dark.png` (yeni —
  WaiterPwa'nın kendi dosyasının birebir kopyası; bu iki statik uygulama
  arasında paylaşılan bir modül/yol yok, WaiterPwa/Cashier'ın zaten
  kurulu "no shared JS module" deseniyle aynı, statik varlıklar için de
  geçerli)
- `evidence/V1-RMD-255/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
  (birikimli olarak birçok görevin sahipliğinde) — `.pos-logo` içindeki
  ikon+metin gerçek logo `<img>`'ıyla değiştirilir, artık kullanılmayan
  `#ico-brand` sprite tanımı kaldırılır. Başka hiçbir öğe değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
  (V1-CUI-008 sahipliğinde kalır; V1-RMD-252/253 de burada aynı desenle ek
  yapmıştı) — WaiterPwa'nın `.brand-mark` kuralıyla birebir aynı
  (`height:24px; width:auto; display:block;`) bir kural eklenir.

## Dependencies

- None

## Acceptance evidence

- `tests/Clients/StaticApps/cashier-app.test.js` değişiklik öncesi ve
  sonrası yeşil kalır (mevcut testler `.pos-logo` içeriğine bağlı değil).
- Gerçek Chromium ile önce/sonra ekran görüntüsü: başlıkta artık gerçek
  ALKAROS logosu var, ikon/metin kombinasyonu yok.
- `dotnet build`/`dotnet test`: bu ortamda .NET SDK kurulu değil (aynı
  V1-RMD-252/253/254'teki gibi) — Owned surface yine yalnız statik
  dosyalar.
- `python tools/plan-audit/plan_audit_tool.py validate` → yeni hata yok.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: Cashier'ı aç, başlıkta gerçek
  ALKAROS logosunu gör (WaiterPwa'daki ile aynı görsel).

## Handoff

- None
