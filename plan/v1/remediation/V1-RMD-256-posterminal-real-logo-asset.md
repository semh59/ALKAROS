# V1-RMD-256 - Give PosTerminal's shell header the real ALKAROS logo

- Task ID: V1-RMD-256
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-255'in devamı: dört istemciden yalnızca WaiterPwa gerçek bir logo
dosyasına sahipti. PosTerminal'in `ProductionShell.tsx`'i başlıkta
`<div className="production-shell__brand">ALKAROS</div>` — düz metin,
hiçbir görsel/ikon yok. Bu görev PosTerminal'i kapsıyor; CustomerWeb
(müşteriye açık ekran, ayrı bir ürün kararı gerektiriyor) hâlâ kapsam
dışı.

PosTerminal React/Vite ile derleniyor (WaiterPwa/Cashier'ın statik
HTML/JS'inden farklı bir hat) — bu yüzden V1-RMD-255'ten ayrı bir görev.
Header zemini `var(--ds-color-brand-strong)` (`color-mix(in srgb, #1b4d7b
82%, black)`, doğrulandı: `tokens.css:29`) — WaiterPwa/Cashier'ın
`--color-ink`'inden bile daha koyu, "on-dark" logo varyantı burada da
sorunsuz okunur.

## Owned surface

- `src/Clients/PosTerminal/src/design-system/brand/alkaros-logo-on-dark.png`
  (yeni — WaiterPwa'nın dosyasının birebir kopyası; PosTerminal'in kendi
  derleme hattı, başka bir istemciyle paylaşılan asset yolu yok)
- `evidence/V1-RMD-256/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/shell/ProductionShell.tsx
  (V1-RMD-042 sahipliğinde) — yalnız marka `<div>`'inin içeriği `ALKAROS`
  metninden gerçek logo `<img>`'ına değişir (`import` eklenir); `aria-label`
  eklenmez (mevcut a11y testi bunu doğruluyor). Başka hiçbir prop/mantık
  değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/shell/shell.css
  (V1-RMD-025 sahipliğinde) — yeni bir `.production-shell__brand-mark`
  kuralı eklenir (WaiterPwa/Cashier'ın `.brand-mark` kuralıyla aynı:
  `height:24px; width:auto; display:block;`). Mevcut `.production-shell__brand`
  kuralları (responsive `display:none` dahil) değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/shell/ProductionShell.test.tsx
  (V1-RMD-023 sahipliğinde kalır) — yeni bir assertion, marka alanının
  artık `src`/`alt="ALKAROS"` taşıyan bir `<img>` olduğunu doğrular.

## Dependencies

- None

## Acceptance evidence

- `pnpm test` (PosTerminal'in tüm vitest paketi): gerçek baseline
  189/189 DEĞİL — `src/stale.test.ts`'teki "gives the pairing dialog an
  accessible modal contract" testi, `git stash` ile değişikliklerim
  tamamen geri alınmış ORİJİNAL kodda da (3 ayrı çalıştırmada, hem tek
  başına hem tam paket içinde) aynı şekilde başarısız oluyor — bu görevden
  önce de var olan, ilgisiz bir bulgu (bkz. `evidence/V1-RMD-256/`).
  Değişikliğimle birlikte sonuç birebir aynı: 188/189, tek başarısız aynı
  test. Kendi eklediğim assertion'ın bulunduğu `ProductionShell.test.tsx`
  ayrı çalıştırıldığında 12/12 yeşil.
- `ProductionShell.test.tsx`'teki mevcut a11y/aria-label assertion'ı hâlâ
  geçer; yeni bir assertion, marka alanında artık `src`/`alt="ALKAROS"`
  taşıyan bir `<img>` olduğunu doğrular.
- Gerçek Chromium ile görsel kanıt: `ProductionShell`'i gerçek test
  prop'larıyla izole bir Vite sayfasında render edip ekran görüntüsü al
  (canlı backend bu ortamda yok, tam login akışı gerektirmeden component'in
  kendisini doğrulamak için) — görüntü `evidence/V1-RMD-256/**`'a konur,
  harness dosyaları committed değil.
- `pnpm build` (`tsc --noEmit && vite build`): temiz derleme, yeni asset
  import'u tip hatası vermez (`vite/client` tipleri zaten `tsconfig.json`'da
  kayıtlı).
- `python tools/plan-audit/plan_audit_tool.py validate` → yeni hata yok.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Not: bu ortamda Node sürümü v22.22.2, `package.json`'ın istediği
  `>=24.0.0`'ın altında (`pnpm install` bunu uyarı olarak bildirdi, hata
  değil) — testler/build yine de çalıştı, ama bu fark kayıt altına
  alınıyor.
- Semih'in elle deneyebileceği senaryo: PosTerminal'de oturum aç, başlıkta
  artık gerçek ALKAROS logosunu gör (WaiterPwa/Cashier'daki ile aynı görsel).

## Handoff

- None
