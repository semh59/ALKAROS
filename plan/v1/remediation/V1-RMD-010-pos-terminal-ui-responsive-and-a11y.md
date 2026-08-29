# V1-RMD-010 - PosTerminal UI touch targets, accessibility, contrast and responsive states

- Task ID: V1-RMD-010
- Status: Done
- Assignee: 2814a35e-cc66-4d6c-882c-1b1809271ab4
- Work type: validation
- Surface state: Existing

## Goal

V1-RMD-020 tarafından compose edilen integrated production shell'i dokunma hedefi, WCAG 2.2 AA, keyboard/focus,
required states ve dokuz viewport responsive kabul matrisiyle yeniden doğrulamak.

## Owned surface

- `evidence/V1-RMD-010/**`

## In scope

- 1920x1080'den 320x568'e kadar dokuz hedef ekran ve breakpoint ±1 px sınırında integrated shell'i doğrulamak.
- 44x44 px targets, WCAG 2.2 AA, keyboard-only, focus order/restoration, named modal, Escape ve reduced-motion
  davranışlarını browser kanıtıyla yeniden kabul etmek.
- Loading, empty, busy, success, error, offline, stale, unauthorized, conflict ve domain-specific state'leri table,
  catalog, kitchen/operations, order ve customer-display yüzeylerinde doğrulamak.

## Out of scope

- Production source, backend API veya contract değiştirmek; finding çıkarsa exact yeni remediation task'ı açılır.
- WebPrototype istemcisini değiştirmek veya mock davranışı production evidence saymak.

## Dependencies

- V1-RMD-020
- V1-RMD-025

## Deliverables

- Dokuz viewport/breakpoint, required-state ve accessibility kanıt paketi.
- Integrated shell için açık pass/fail reacceptance özeti; source finding'i varsa exact yeni task blocker'ı.

## Acceptance evidence

- 1920x1080, 1440x900, 1366x768, 1280x800, 1024x768, 768x1024, 430x932, 390x844 ve 320x568 ile her breakpoint
  ±1 px için screenshot, bounding-box, DOM/a11y, focus-order, console/network ve overflow kanıtı üretilir.
- Tüm kritik controls gerçek bounding box'ta >=44x44 px; focus indicator hem açık hem koyu zeminde >=3:1; modal adı,
  focus trap, Escape ve tetikleyiciye focus restoration browser testiyle doğrulanır.
- Keyboard-only tamamlama, 200/400% zoom, reduced-motion ve WCAG 2.2 AA kontrastı geçer; otomatik a11y taramasında
  sıfır critical/serious ihlal olur.
- Loading, empty, busy, success, error, offline, stale, unauthorized, conflict, submitted, Paying, Completed,
  Unavailable ve retry state'leri ayrı görüntülenir; stale parasal veri <=10.000 ms'de temizlenir.
- `pnpm test`, `pnpm typecheck` ve `pnpm build` exit code 0 verir.
- `evidence/V1-RMD-010/**` altında viewport ekran görüntüleri ve raporlar saklanır.
- Semih gerçek integrated shell üzerinde keyboard/touch ile login olur; masa seçip sipariş girer, pairing/retry/offline
  durumlarını dener ve kritik CTA, total, connectivity ile focus davranışını dokuz viewport içinde doğrular.

## Handoff

- V1-RMD-011
