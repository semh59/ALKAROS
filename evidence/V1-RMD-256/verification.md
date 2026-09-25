# V1-RMD-256 — Verification transcript

## Ortam

- Node v22.22.2 (paket `>=24.0.0` istiyor — `pnpm install` uyarı verdi,
  hata değil; testler/build yine de çalıştı, ama fark kayıt altına
  alınıyor).
- pnpm 11.19.0, `pnpm install` ile bağımlılıklar kuruldu.
- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`, Playwright 1.56.1).

## Değişiklik

- `src/design-system/brand/alkaros-logo-on-dark.png` — WaiterPwa'nın
  aynı dosyasının birebir kopyası.
- `ProductionShell.tsx`: `import alkarosLogo from
  "../design-system/brand/alkaros-logo-on-dark.png";` eklendi;
  `<div className="production-shell__brand">ALKAROS</div>` yerine
  `<div className="production-shell__brand"><img
  className="production-shell__brand-mark" src={alkarosLogo}
  alt="ALKAROS" /></div>`.
- `shell.css`: `.production-shell__brand-mark { height:24px; width:auto;
  display:block; }` eklendi (WaiterPwa/Cashier'ın `.brand-mark` kuralıyla
  aynı). Mevcut `.production-shell__brand` responsive kuralları
  (`display:none` @319px dahil) değişmedi.
- `ProductionShell.test.tsx`: marka alanının artık `src`/`alt="ALKAROS"`
  taşıyan bir `<img>` olduğunu doğrulayan 3 yeni assertion eklendi.

## Otomatik testler

```text
$ pnpm test
 Test Files  1 failed | 24 passed (25)
      Tests  1 failed | 188 passed (189)
```

**Bulunan ve doğrulanmış, bu görevle ilgisiz bir başarısızlık:**
`src/stale.test.ts > ... > gives the pairing dialog an accessible modal
contract` — `document.querySelectorAll("button")` içinde "Ekranı eşleştir"
metinli bir buton bulamıyor (`expect(trigger).toBeDefined()` başarısız).

**Revert-and-confirm ile kök neden izole edildi:** `git stash` ile TÜM
değişikliklerim (bu görevin dosyaları dahil) geçici olarak geri alındı,
orijinal kodda bu test 3 ayrı çalıştırmada (`src/stale.test.ts` tek başına
× 3, sonra tam `pnpm test` paketiyle × 1) HER SEFERİNDE aynı şekilde
başarısız oldu. `git stash pop` ile değişikliklerim geri getirildi, sonuç
birebir aynı (188/189, aynı tek test). Bu, benim değişikliğimin
YARATMADIĞI, önceden var olan bir bulgu — muhtemelen `App`/router'ın
async render sırası ile ilgili bir zamanlama sorunu (bu ortamdaki Node
sürüm farkıyla da ilgili olabilir, doğrulamadım). Bu görevin kapsamında
düzeltilmedi.

`ProductionShell.test.tsx` tek başına: 12/12 yeşil (yeni assertion dahil).

## `pnpm build` (tsc --noEmit && vite build)

```text
✓ 101 modules transformed.
dist/assets/alkaros-logo-on-dark-CmVDjlL_.png   32.94 kB
✓ built in 462ms
```

Temiz derleme, tip hatası yok, Vite yeni PNG'yi kendi asset hattından
gerçekten işleyip hashli bir dosya olarak `dist/assets/`'e yazdı.

## Gerçek tarayıcı doğrulaması

Bu ortamda canlı bir backend olmadığı için gerçek `/` yoluna gidildiğinde
(giriş ekranı, `pos-baseline.png`) `ProductionShell` hiç render olmuyor —
tam login akışı gerekiyor. Bunun yerine, `ProductionShell.test.tsx`'in
kullandığı BİREBİR AYNI mock prop'larla component'i izole bir Vite
sayfasında (`harness.html`/`harness-main.tsx`, GEÇİCİ — committed değil,
bu doğrulamadan hemen sonra silindi) gerçek Chromium'da render edip
ölçüldü:

```text
logo: {"naturalWidth":1005,"naturalHeight":233,"complete":true,"alt":"ALKAROS"}
```

`after.png` — gerçek ALKAROS logosu, gerçek header zemininde
(`--ds-color-brand-strong`) net okunuyor, WaiterPwa/Cashier ile aynı görsel.

## Proje-geneli kontroller

- `python tools/plan-audit/plan_audit_tool.py validate` → 1 hata
  (`C54_APPLICATION_ADMISSION_V3_FINAL_MISSING`), önceden de vardı, ilgisiz.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`.
