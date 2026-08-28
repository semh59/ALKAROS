# V1-RMD-025 - Production shell zoom reflow recovery

- Task ID: V1-RMD-025
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

Production shell'i gerçek %200 ve %400 browser zoom altında kritik header kontrollerini kırpmadan ve fixed shell
katmanları ana içeriği kapatmadan reflow edecek biçimde, temiz write-set ve evidence-scoped bundle ile düzeltmek.

## Owned surface

- `src/Clients/PosTerminal/src/shell/shell.css`
- `src/Clients/PosTerminal/src/shell/layout-contract.test.ts`
- `evidence/V1-RMD-025/**`

## Dependencies

- V1-GOV-016
- V1-RMD-023

## Acceptance evidence

- %200 gerçek Chrome ölçümünde `Müşteri ekranı` ve `Çıkış` görünür client sınırı içinde kalır; yatay overflow veya
  clipped unreachable control oluşmaz.
- %400 gerçek Chrome ölçümünde banner, sistem durumu ve ana navigasyon fixed/sticky overlap üretmez; ana içerik ve
  kritik workflow yalnız dikey scroll ile erişilebilir kalır.
- Tüm interactive targets en az 44x44 CSS px kalır; 320x568 normal viewport, breakpoint ±1 ve reduced-motion
  davranışları gerilemez.
- Layout contract testleri, `pnpm test` ve `pnpm typecheck` exit code `0` verir. Production bundle yalnız
  `pnpm --dir src/Clients/PosTerminal exec vite build --outDir D:/PROJECT/ALKAROS/evidence/V1-RMD-025/browser-dist
  --emptyOutDir` komutuyla owned evidence dizinine yazılır; `src/Clients/PosTerminal/dist/**` değiştirilmez.
- Evidence-scoped bundle gerçek HTTPS Host ve fresh digest-pinned PostgreSQL 18 üzerinde %200/%400 Chrome testini,
  screenshot/bounding-box/console kayıtlarını ve zoom'un %100'e geri yüklendiğini kanıtlar.
- Semih production `Masalar` ekranında %200 ve %400 zoom ile header action, masa seçimi, hızlı masa aksiyonu, ana CTA
  ve connectivity/freshness durumlarına keyboard/touch ile dikey scroll üzerinden erişir.

## Handoff

- V1-RMD-010
