# V1-RMD-024 - Production shell zoom reflow

- Task ID: V1-RMD-024
- Status: Blocked
- Assignee: /root
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

## Blocker

- İlk production build komutu `--outDir` argümanını Vite'a yanlış iletti ve owned surface dışındaki
  `src/Clients/PosTerminal/dist/**` artifact'larını yeniden üretti. Bu write-set ihlali görev kapanış kapısını
  başarısız yapar; generated dosyaların ignored olması kapsam izni üretmez.
- Önceki bundle, owned CSS ekimi geçici olarak kaldırılarak deterministik biçimde yeniden üretildi;
  `index-CgU1nhAH.js` ve `index-Ce2tG2yq.css` preflight artifact adları geri geldi. Owned source/test davranış ekleri
  kaldırıldı, fakat patch tooling byte düzenini değiştirdiği için başlangıç SHA-256 değerleri birebir dönmedi. Bu
  recovery ihlali gizlemez veya görevi `Done` yapmaz.
- Görev ancak production bundle output'u için exact custody kuran ayrı governance/integration zinciri ve taze
  write-set ile yeniden uygulanıp doğrulanan yeni remediation görevi tamamlandıktan sonra açılabilir. Kanıt:
  `evidence/V1-RMD-024/scope-failure-2026-08-27.md`.

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
