# V1-RMD-024 - Production shell zoom reflow

- Task ID: V1-RMD-024
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Existing

## Goal

Production shell'i gerçek %200 ve %400 browser zoom altında kritik header kontrollerini kırpmadan ve fixed shell
katmanları ana içeriği kapatmadan reflow edecek biçimde düzeltmek.

## Owned surface

- `evidence/V1-RMD-024/**`

## Dependencies

- V1-GOV-015
- V1-RMD-023

## Acceptance evidence

- %200 gerçek Chrome ölçümünde `Müşteri ekranı` ve `Çıkış` görünür client sınırı içinde kalır; yatay overflow veya
  clipped unreachable control oluşmaz.
- %400 gerçek Chrome ölçümünde banner, sistem durumu ve ana navigasyon birbirini ya da ana içeriği kapatmaz; bütün
  bölgeler yalnız dikey scroll ile erişilir ve ana workflow için görünür alan bırakılır.
- Tüm interactive targets en az 44x44 CSS px kalır; 320x568 normal viewport, breakpoint ±1 ve reduced-motion
  davranışları gerilemez.
- Layout contract testleri, `pnpm test`, `pnpm typecheck`, `pnpm build`, plan validation ve `git diff --check` exit code
  `0` verir.
- Semih production `Masalar` ekranında %200 ve %400 zoom ile header action, masa seçimi, hızlı masa aksiyonu, ana CTA
  ve connectivity/freshness durumlarına keyboard/touch ile dikey scroll üzerinden erişir.

## Handoff

- V1-RMD-025
