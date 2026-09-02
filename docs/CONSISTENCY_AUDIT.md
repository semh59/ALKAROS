# Consistency Audit

`tools/consistency-audit/consistency_audit.py` is a zero-dependency scan that
guards three invariants across the codebase:

- Code and schema identities stay English-only.
- Known English terms do not leak into user-facing Turkish UI text.
- User-facing role text is sourced from the central catalog, not inline literals.

It is deliberately narrow so it produces no false positives on a clean tree and
can gate every remediation wave.

## What it checks

| Check | Scope | Rule |
| --- | --- | --- |
| Migration identities | `database/migrations/**/*.sql` | No Turkish characters (`ş ğ ı ö ü ç` and capitals) on any line. |
| Code identifiers | `src/**/*.{cs,ts,tsx}` (excludes `bin`, `obj`, `node_modules`, `dist`) | No Turkish characters right after `class`, `interface`, `record`, `enum`, `struct`, `namespace`, `func`, `function`, `const`, `let`, `var`, `type`, `def`. |
| Code comments | `src/**/*.{cs,ts,tsx}` comment lines (`//`, `///`, `*`, `#`) | No Turkish characters, except the currency proper noun `kuruş` / `kurus`. |
| UI term leaks | `src/Clients/**/*.{cs,ts,tsx}` non-comment lines | `Catalog` or `Unknown` must not appear inside `aria-label=`, `title=`, `placeholder=` attributes or as `Catalog ara` / `Unknown ara` visible text. |
| Role noun leaks | `src/Clients/**/*.{ts,tsx}` non-comment lines, excluding `strings.ts` and `*.test.*` | `Manager`, `Supervisor` or `Cashier` must not appear as a whole word inside a quoted string literal. Role text comes from the central catalog `src/Clients/PosTerminal/src/strings.ts`. |

User-facing Turkish string literals are **not** flagged; that is the desired
state. See `docs/UI_STYLE_GUIDE.md` for the Turkish terminology dictionary the
UI-leak check is derived from.

## How to run

```sh
python tools/consistency-audit/consistency_audit.py
```

Exit code `0` and `consistency-audit: clean` mean no violations. A non-zero exit
prints each violation as `path:line: reason`.

## When to run

- Before opening every `V1-RMD` remediation wave and before every version gate
  reseal (`V1-GOV-*` closure tasks include it in their acceptance evidence from
  wave 10 onward).
- After any change that adds schema, identifiers or client UI strings.

## Extending the term list

New banned UI terms and their Turkish replacements are added to
`docs/UI_STYLE_GUIDE.md` first, then mirrored into `LEAK_RE` in the script. Keep
the pattern anchored to UI attributes so it stays false-positive free.
