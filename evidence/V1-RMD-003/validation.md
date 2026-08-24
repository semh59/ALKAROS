# V1-RMD-003 validation evidence

- Baseline commit: `58ae1318822c3e255cb3d46916855a29867ee7ee`
- Validation date: `2026-08-24`
- Repository root: `D:/PROJECT/ALKAROS`

## Automated checks

| Check | Result |
| --- | --- |
| Bundled Node `--check src/Clients/WebPrototype/app.js` | exit `0` |
| Bundled Node `--test src/Clients/WebPrototype/tests/*.test.js` | exit `0`; 8 passed, 0 failed |
| `python -B tools/plan-audit/plan_audit_tool.py validate` | exit `0`; 0 errors, 0 warnings |
| `python -B tools/task-scope/task_scope_tool.py --task-id V1-RMD-003 --format text` | exit `0`; all changes within scope |
| `git diff --check` | exit `0` |
| `.NET SDK 10.0.302 dotnet build ALKAROS.slnx --no-restore` | exit `0`; 0 warnings, 0 errors |

## Browser verification

| Viewport | Surface | Horizontal overflow | Touch targets below 44x44 px |
| --- | --- | --- | --- |
| 1440x900 | Cashier tables and redesigned order workspace | No | 0 |
| 1024x768 | Cashier tables | No | 0 |
| 768x1024 | Cashier tables; waiter tablet tables/order | No | 0 |
| 390x844 | Cashier tables; waiter phone tables/order/cart | No | 0 |

- Browser console: 0 errors, 0 warnings.
- Visible text below 12 px at 1440x900: 0.
- Dialogs with accessible name: 13/13.
- Named modal close buttons: 12/12.
- Named icon-only buttons: 17/17.
- Native button table cards: 28/28.
- Native button product cards: 20/20.
- Modifier dialog initial focus, Escape close, focus trap and trigger focus restoration verified.
- Cashier tables and order workspace light/dark visible-text contrast scan: 0 elements below the applicable 4.5:1
  normal-text or 3:1 large-text threshold; measured minimum order ratios 4.84:1 light and 4.68:1 dark.
- Waiter phone and tablet views keep 14 table cards and 10 product cards available without horizontal overflow; compact
  prototype tools do not overlap the order submit action or bottom navigation.
- Dark printer warning foreground/background contrast: 7.28:1.

## Artifact hashes

| SHA-256 | Path |
| --- | --- |
| `E8ECEC24CDC7A842AA95AC10A18D1E3410BADBFFBD32E40CBED7C08C600B15AD` | `src/Clients/WebPrototype/index.html` |
| `4BDD1B757F0A23A4DA51E93F590D4E3F720CB970FA252A6B31EB285D6DDA6276` | `src/Clients/WebPrototype/styles.css` |
| `75AF2C8E6EF06D5DDEAB89214CBC2C8CFD61F0517EBF7570257408CF709B23BA` | `src/Clients/WebPrototype/app.js` |
| `E543B73A354FABC993FECFF01B3FEED77A9438CCB1798FE3C1BA08574F12063D` | `src/Clients/WebPrototype/tests/app.security.test.js` |
| `EF557184744CE58C03AA546AE7183FC075C1FBA9FA30DBC2B49D2018AE8C6B9B` | `src/Clients/WebPrototype/tests/ui.accessibility.test.js` |
