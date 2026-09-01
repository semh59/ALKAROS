# V1-RMD-092 - Device browser test plan and vanilla client a11y smoke

- Task ID: V1-RMD-092
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- EXT:WCAG-2.2

## Goal

Tek bir `docs/qa/device-browser-test-plan.md` dokümanı ile `V0-CMP-005` cihaz/tarayıcı matrisini, her istemcinin otomatik kapsamını (PosTerminal: `ProductionShell` kırılım testi + 8 çalışma alanı axe testi; Cashier / Waiter PWA: bu görevle eklenen shell smoke) ve go-live öncesi fiziksel cihaz manuel kontrol listesini kayıt altına almak. Cashier ve Waiter PWA vanilla-JS istemcileri için `src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts` shell axe smoke'u eklenir; test kritik/ciddi WCAG ihlali bulmamalıdır. Smoke'un ortaya çıkardığı Waiter PWA viewport zoom engeli (`user-scalable=no`, `maximum-scale=1.0`; WCAG 1.4.4 Resize Text AA) düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-092-device-browser-test-plan-and-vanilla-client-a11y-smoke.md`
- `src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts`
- `src/Clients/WaiterPwa/wwwroot/index.html`
- `docs/qa/**`
- `evidence/V1-RMD-092/**`

## In scope

- `src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts`: her vanilla istemci `index.html`'i için `lang`, `title`, zoom-engellemeyen viewport meta, en az bir landmark ve axe (`color-contrast` hariç) kritik/ciddi ihlal yokluğu doğrulaması.
- `src/Clients/WaiterPwa/wwwroot/index.html` viewport meta'sından `maximum-scale=1.0` ve `user-scalable=no` kaldırılması; `viewport-fit=cover` korunur.
- `docs/qa/device-browser-test-plan.md`: matris, otomatik kapsam haritası, manuel fiziksel cihaz kontrol listesi, `color-contrast`'ın jsdom sınırlaması notu ve `V0-CMP-005` seviye kararının onay beklediği notu.

## Out of scope

- Playwright/gerçek tarayıcı otomasyonu.
- Cashier `wwwroot` değişikliği; shell smoke'u temiz geçer.
- PosTerminal mevcut axe/kırılım testleri.
- WCAG seviye kararı onayı; Semih'e aittir.

## Dependencies

- V1-GOV-060

## Deliverables

- `src/Clients/PosTerminal/src/vanilla-clients-a11y.test.ts` ve düzeltilmiş `src/Clients/WaiterPwa/wwwroot/index.html`.
- `docs/qa/device-browser-test-plan.md`.
- `evidence/V1-RMD-092/` altında öncesi/sonrası axe çıktısı.

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal test` yeni smoke dahil sıfır hata verir.
- Waiter PWA `index.html` viewport meta'sı `user-scalable=no` / `maximum-scale=1` içermez; axe `meta-viewport` ihlali kalkmış olur.
- `docs/qa/device-browser-test-plan.md` matrisi, otomatik kapsamı ve manuel kontrol listesini içerir.

## Handoff

- V1-GOV-061
