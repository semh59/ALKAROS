# V1-CUI-008 - Cashier vanilla JS uygulamasını Faz 0 token'larına taşı

- Task ID: V1-CUI-008
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`src/Clients/Cashier/wwwroot/cashier-app.css` içindeki Faz 0 öncesi token
seti (`--bg-primary`, `--text-main`, `--bg-surface-elevated`,
`--border-color` vb.) `docs/design/foundations.md`'nin kilitli
`--color-ink/--color-brand/--color-accent/--color-text/--color-canvas/
--color-surface/--color-border` paletine taşımak; HTML yapısı ve
`cashier-app.js` davranışı değişmeden yalnız görsel dili güncellemek.

## Owned surface

- `src/Clients/Cashier/wwwroot/cashier-app.css`
- `tests/Clients/Cashier/Frontend/**`
- `evidence/V1-CUI-008/**`
- Bu görev, `src/Clients/Cashier/wwwroot/index.html`, `cashier-app.js` ve
  `manifest.json` dosyalarına dokunmaz (V1-RMD-083 sahipliğinde kalır).

## In scope

- Token tanımlarının ve kullanım noktalarının Faz 0 paletine geçişi.
- `--color-accent` dolgu/metin kontrast kuralına (foundations.md §1.1)
  uyum doğrulaması.

## Out of scope

- HTML yapısı, JS davranışı, akış/etkileşim değişikliği.
- İkon sprite güncellemesi (ayrı, gerekirse ayrı görev).

## Dependencies

- V1-CUI-007

## Acceptance evidence

- `dotnet build src/Clients/Cashier/ALKAROS.Cashier.csproj -c Debug` → 0 Uyarı, 0 Hata.
- `pytest tests/Clients/Cashier/Frontend/test_cashier_frontend.py` → 4/4 geçti (HTML/JS içeriği bu görevde değişmedi, testler zaten CSS'e bakmıyor).
- `npx vitest run src/vanilla-clients-a11y.test.ts` (PosTerminal, Cashier+WaiterPwa shell'lerini axe ile denetliyor) → 6/6 geçti, yeni kontrast ihlali yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Değişiklik yalnız `src/Clients/Cashier/wwwroot/cashier-app.css` (git diff ile doğrulandı) — `index.html`/`cashier-app.js`'e dokunulmadı.
- Yapılanlar: tüm eski token'lar (`--bg-*`, `--text-*`, `--accent-*`, `--border-color`, `--border-focus`) ve dosyaya saçılmış ham hex/rgba değerleri Faz 0 paletine taşındı; `.category-tab-btn.active` artık accent dolgu + ink metin (eskiden accent dolgu + beyaz metin — kontrast kuralına aykırıydı); `.pos-product-price`/`.grand-total-amount` artık accent-as-text kullanmıyor (sırasıyla brand/ink); dispatch butonu accent yerine success; session pill metni artık renklendirilmiyor, yalnız durum noktası (dot) renkleniyor.

**Blocker/takip notu (Done'ı engellemiyor, kapsam dışı bırakıldı):**
foundations.md §2'nin Inter yazı tipi kararı bu görevde UYGULANMADI — Inter
bir webfont, yüklenmesi `index.html`'e bir Google Fonts `<link>` eklemeyi
gerektiriyor, ve bu dosya bu görevin allowlist'i dışında (V1-RMD-083
sahipliğinde). `--font-sans`/`--font-mono` bilerek system-ui yığınında
bırakıldı. İsterseniz `index.html`'i de kapsayan ayrı bir görev (V1-RMD-083'ten
devir) açılabilir.
