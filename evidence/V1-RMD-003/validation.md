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
| `dotnet build ALKAROS.slnx --no-restore` | not started; `dotnet` executable unavailable |

## Browser verification

| Viewport | Surface | Horizontal overflow | Touch targets below 44x44 px |
| --- | --- | --- | --- |
| 1440x900 | Cashier tables/order/menu/operations | No | 0 |
| 1024x768 | Cashier tables | No | 0 |
| 768x1024 | Waiter tablet tables/order | No | 0 |
| 390x844 | Waiter phone tables/order/cart | No | 0 |

- Browser console: 0 errors, 0 warnings.
- Visible text below 12 px at 1440x900: 0.
- Dialogs with accessible name: 13/13.
- Named modal close buttons: 12/12.
- Named icon-only buttons: 17/17.
- Native button table cards: 28/28.
- Native button product cards: 20/20.
- Modifier dialog initial focus, Escape close, focus trap and trigger focus restoration verified.
- Light and dark visible-text contrast scan: 0 elements below 4.5:1.
- Dark printer warning foreground/background contrast: 7.28:1.

## Artifact hashes

| SHA-256 | Path |
| --- | --- |
| `400A2494FBA5116D36D8705BD2D0F824D10B2612A79C73E6CC97E122F6C722A9` | `src/Clients/WebPrototype/index.html` |
| `1BED4008A151D9BE57ED47180525BB2C54D97B5269F371A195A890251304A634` | `src/Clients/WebPrototype/styles.css` |
| `0F710181080510959D0BF77541D045D47EE761208C1C06DBA85A4CF4327D329A` | `src/Clients/WebPrototype/app.js` |
| `E543B73A354FABC993FECFF01B3FEED77A9438CCB1798FE3C1BA08574F12063D` | `src/Clients/WebPrototype/tests/app.security.test.js` |
| `EF557184744CE58C03AA546AE7183FC075C1FBA9FA30DBC2B49D2018AE8C6B9B` | `src/Clients/WebPrototype/tests/ui.accessibility.test.js` |
