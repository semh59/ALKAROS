# V1-RMD-238 - Escape quote characters in Cashier's escapeHtml

- Task ID: V1-RMD-238
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`cashier-app.js`'in `escapeHtml` fonksiyonu (`div.textContent = text; return
div.innerHTML` yöntemi) yalnız `&`/`<`/`>` karakterlerini kaçırıyor, `"` ve
`'` karakterlerini KAÇIRMIYOR. Aynı dosyada kullanıcı girdisi (ürün notu,
çalışan adı) çift-tırnaklı HTML attribute'larına (`value="${escapeHtml(
item.note)}"` gibi) basılıyor — bir ürün notu `" onmouseover="..."` içerirse
attribute'u kapatıp kasiyerin kendi oturumunda çalışan bir event handler
enjekte edebilir. Bağımsız bir denetim ajanı (2026-09-18, tüm proje kod
denetimi, frontend clients) bunu tespit etti: AYNI zafiyet WaiterPwa'da
daha önce bulunup düzeltilmişti (`js/util.js`, gerçek bir olayla — bir ürün
adı `Kola" onmouseover="..."` şeklinde attribute'u kapatmıştı), ama bu
düzeltme kardeş uygulama Cashier'a hiç yayılmamıştı.

## Owned surface

- `evidence/V1-RMD-238/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
  (birikimli olarak birçok görevin sahipliğinde) — yalnız `escapeHtml`
  fonksiyonu, WaiterPwa'nın `js/util.js`'teki zaten doğrulanmış
  `HTML_ESCAPES` regex-replace desenine dönüştürülür; hiçbir çağıran yer
  veya başka davranış değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/cashier-app.test.js
  (birikimli olarak birçok görevin sahipliğinde) — yeni bir regresyon
  testi eklenir, mevcut testler değişmez.

## In scope

- Yalnız `escapeHtml`'in kendisi; `&`, `<`, `>`, `"`, `'` karakterlerinin
  hepsini kaçıracak şekilde.

## Out of scope

- WaiterPwa/PosTerminal'deki eşdeğer kodun denetlenmesi (WaiterPwa zaten
  düzeltilmiş; PosTerminal React kullanıyor, JSX otomatik kaçırıyor,
  ayrı bir denetim ajanı tarafından zaten doğrulandı).
- `escapeHtml`'in kendisi dışında Cashier'daki başka bir dosya/davranış.

## Dependencies

- None

## Acceptance evidence

- Gerçek jsdom testi: sepete eklenen bir ürünün not alanına `"
  onmouseover="..."` içeren bir metin girilip DOM yeniden render
  edildiğinde, render edilen `<input>` üzerinde enjekte edilmiş bir
  `onmouseover` attribute'u OLUŞMAZ; `value` özelliği orijinal metni
  aynen (kaçırılmış ama bozulmamış) taşır.
- `corepack pnpm --dir src/Clients/PosTerminal vitest run` kapsamı bu
  dosyayı içermiyor — Cashier'ın kendi vitest koşucusuyla (bkz. mevcut
  `tests/Clients/StaticApps/cashier-app.test.js` çalıştırma komutu) tüm
  dosya yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
