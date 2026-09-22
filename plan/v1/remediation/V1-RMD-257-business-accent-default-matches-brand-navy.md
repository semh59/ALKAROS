# V1-RMD-257 - Business accent palette default matches the product's own brand navy

- Task ID: V1-RMD-257
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in bulgusu: bir işletme henüz kendi rengini seçmemişken (varsayılan
kurulum), QR sayfaları `BusinessAccentPalette.DefaultKey` olan "amber"
(`#9C6323`, koyu kahverengi) ile açılıyordu — Cashier/WaiterPwa/PosTerminal'in
üçünün de tasarım sisteminde zaten kullandığı `--color-brand`
(`#1B4D7B`, "lacivert") ile hiç ilgisi olmayan bir renk
(`docs/design/foundations.md` §1; `cashier-app.css`/`waiter-app.css`'in
`--color-brand: #1B4D7B`, PosTerminal'in `--ds-color-brand: #1b4d7b`).
Sonuç: personel ekranları (ALKAROS'un kendi navy markası) ile müşteri
sayfası (alakasız bir amber/kahverengi) aynı ürünmüş gibi görünmüyordu.
"Menü amber değilde diğerleriyle aynı renkleri paylaşsın" — varsayılan artık
paletteki "lacivert" (`#1B4D7B`), zaten ürünün geri kalanının kullandığı
tam olarak aynı hex.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/BusinessIdentity/BusinessAccentPalette.cs
  (V1-SET-007 sahipliğinde) — `DefaultKey`, `"amber"`'dan `"lacivert"`'e
  değişti; palet listesinin kendisi (8 renk, hiçbiri eklenmedi/çıkarılmadı)
  değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Menu/wwwroot/menu-app.css,
  src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.css,
  src/Apps/CustomerWeb/Bill/wwwroot/bill.css (V12-CWB-001/V12-CWB-002/V1-WTR-018
  sahipliğinde) — sabit `--cw-accent` CSS varsayılanı da `#1b4d7b`'ye
  güncellendi; aksi hâlde `loadBranding()`'in (V1-SET-009) fetch'i
  tamamlanana kadar sayfa bir an eski kahverengiyle açılıp sonra laciverte
  geçerdi (flash of unstyled color) — artık ilk boyamadan itibaren doğru.
- `evidence/V1-RMD-257/**` (yeni)

## Out of scope

- Palete yeni bir renk eklemek/çıkarmak — yalnızca hangi rengin varsayılan
  olduğu değişti.
- `#9C6323` ("amber") hâlâ paletin bir üyesi — bir işletme isterse onu
  bilerek seçebilir, yalnızca artık varsayılan değil.

## Dependencies

- V1-SET-007
- V1-SET-009

## Acceptance evidence

- `dotnet test tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj`
  ve `tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj`:
  tüm testler `BusinessAccentPalette.DefaultKey`'e dinamik referans veriyor
  (sabit `"amber"` literal'i hiçbir testte hardcoded değildi) — regresyonsuz
  yeşil.
- `python -m pytest tests/Apps/CustomerWeb -q`: regresyonsuz yeşil.
- Gerçek Chromium (Playwright) ile: hiç ayar yapılmamış bir kurulumda
  Menu/OrderEntry/Bill'in üçünün de artık `#1B4D7B` (lacivert) gösterdiği,
  eski `#9C6323`/`#B5772F` değil, ekran görüntüsüyle doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: hiç `business.accent_theme`
  ayarlanmamış bir kurulumda `GET /api/v1/qr/branding`'in
  `accentColor:"#1B4D7B"` döndürdüğünü, QR sayfalarının Cashier/WaiterPwa/
  PosTerminal ile aynı laciverti gösterdiğini gör.

## Handoff

- None
