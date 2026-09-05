# V1-GOV-060 - Master custody reopen and remediation wave 19

- Task ID: V1-GOV-060
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Cihaz/tarayıcı test kapsamı dağınık: PosTerminal için axe erişilebilirlik ve kırılım noktası testleri vardır (`ProductionShell` ve 8 çalışma alanı), `V0-CMP-005` bir cihaz/tarayıcı matrisi ve WCAG 2.2 AA hedefi tanımlar, ancak (1) kapsamı tek bir test planında toplayan ve fiziksel cihaz manuel kontrol listesini içeren bir doküman yoktur, (2) Cashier ve Waiter PWA vanilla-JS istemcilerinin işaretlemesi için hiç otomatik erişilebilirlik smoke'u yoktur. Bu boşlukları kapatmak için `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-092`) ve kapanış görevi (`V1-GOV-061`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-060-master-custody-reopen-and-remediation-wave-19.md`
- `plan/v1/remediation/V1-RMD-092-device-browser-test-plan-and-vanilla-client-a11y-smoke.md`
- `plan/v1/governance/V1-GOV-061-wave19-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 19. dalga görevleriyle (`V1-RMD-092`, `V1-GOV-061`) genişletmek ve sayımı güncellemek.
- Yüzey devri: `src/Clients/WaiterPwa/wwwroot/index.html` yüzeyi `V1-RMD-083`'ten `V1-RMD-092`'ye devredilir.
- `EXT:WCAG-2.2` tüketici listesine `plan/OFFICIAL_SOURCE_REGISTER.md` içinde `V1-RMD-092` eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Playwright veya gerçek tarayıcı otomasyonu; fiziksel cihaz testi manuel kontrol listesi olarak kalır.
- `V0-CMP-005` WCAG seviye kararının onaylanması; bu Semih'in decision görevidir (`docs/compliance/accessibility-target.md` onay bekliyor).
- PosTerminal axe/kırılım testlerini değiştirmek; mevcut kapsam korunur.

## Dependencies

- V1-GOV-059

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-092`) ve kapanış görevi (`V1-GOV-061`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-092
