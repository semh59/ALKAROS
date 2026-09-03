## Building with AlkarosPos (ALKAROS POS Terminal)

This is a small, opinionated set of primitives for a restaurant point-of-sale UI
(Turkish-language product). Compose screens from these 8 components plus your own
layout markup styled with the `--ds-*` custom properties below. There is **no
utility-class system** and **no theme provider** — styling is CSS custom
properties, and the components own their internal classes (`ds-button`,
`ds-field`, `ds-state-message`, …); you do not write those.

### Setup

Load `styles.css` once (it `@import`s tokens, the Inter `@font-face`s, and the
compiled component CSS). No provider, no context — mount components directly.
Put the DS tree in its own root node so it does not collide with the host app's
React root.

```jsx
const { StateMessage, Button, TextField } = window.AlkarosPos;
ReactDOM.createRoot(document.getElementById("ds-root")).render(
  <div style={{ display: "grid", gap: "var(--ds-space-4)", padding: "var(--ds-space-5)", background: "var(--ds-color-canvas)", color: "var(--ds-color-ink)", fontFamily: "var(--ds-font-sans)" }}>
    <h1 style={{ font: "700 1.25rem/1.2 var(--ds-font-sans)", margin: 0 }}>Masa 14</h1>
    <TextField label="Müşteri adı" hint="Adisyon fişinde görünür." placeholder="Ad Soyad" />
    <StateMessage tone="warning" title="Son 3 porsiyon">
      Izgara köfte kritik stokta.
    </StateMessage>
    <div style={{ display: "flex", gap: "var(--ds-space-3)", justifyContent: "flex-end" }}>
      <Button variant="quiet">Vazgeç</Button>
      <Button variant="primary">Siparişi gönder</Button>
    </div>
  </div>
);
```

### The styling idiom — use these tokens for all your own layout/glue

All names are declared in `_ds_bundle.css` (verbatim from upstream). Reference
them as `var(--name)`; never hard-code hexes or pixels that a token covers.

| Group | Tokens |
|---|---|
| Surfaces | `--ds-color-canvas` (page), `--ds-color-surface` (card), `--ds-color-surface-subtle` |
| Text | `--ds-color-ink` (body), `--ds-color-muted` (secondary), `--ds-color-line` (borders) |
| Brand / action | `--ds-color-brand`, `--ds-color-brand-strong`, `--ds-color-accent`, `--ds-color-accent-hover`, `--ds-color-focus` (focus ring) |
| Status (each with a `-soft` bg pair) | `--ds-color-info` / `--ds-color-info-soft`, `--ds-color-success` / `-soft`, `--ds-color-warning` / `-soft`, `--ds-color-danger` / `-soft` |
| Spacing (0.25rem → 2rem) | `--ds-space-1`, `--ds-space-2`, `--ds-space-3`, `--ds-space-4`, `--ds-space-5`, `--ds-space-6` |
| Radius | `--ds-radius-sm` (controls), `--ds-radius-md` (dialogs) |
| Type | `--ds-font-sans` (Inter + system fallback) |
| Elevation | `--ds-shadow-raised` |
| Touch / shell | `--ds-target-min` (44px min tap target — honor it on any custom control), `--ds-shell-header-height`, `--ds-shell-status-height`, `--ds-shell-rail-width`, `--ds-shell-compact-rail-width`, `--ds-shell-drawer-width` |

### Per-component style hooks (there is no other theming surface)

- **Button** — `variant="primary" | "secondary" | "quiet"`. Put an `<Icon>` as the first child for a leading icon; passes through all native `<button>` props (`disabled`, `onClick`, `type`, …).
- **TextField / SelectField** — always give `label`; optional `hint` and `error` (setting `error` paints the invalid state). `SelectField` takes `<option>` children. Both forward native input/select props.
- **StateMessage** — `tone` is the whole visual axis: `info | success | warning | error | offline | stale | conflict | unauthorized | forbidden`. `title` + optional body children + optional `actions` (a node, usually `<Button>`s). Urgent tones get `role="alert"` automatically.
- **ValidationSummary** — `title` + `errors: string[]`; renders nothing when `errors` is empty.
- **ModalDialog** — controlled: `open`, `title`, `onClose`. Renders a fixed backdrop + focus trap; children are the body.
- **ContextDrawer** — `presentation="persistent"` (in-flow side panel) or `"sheet"` (fixed overlay panel, needs `onClose`).
- **Icon** — `name` from the fixed 26-icon set (see `Icon.prompt.md` / `Icon.d.ts`); `label` makes it non-decorative. Sizes with `font-size` (`1em`), colors with `currentColor`.

### Where the truth lives

Read `styles.css` and its `@import` targets (`_ds_bundle.css`, `fonts/fonts.css`)
before styling. For any component, read `components/general/<Name>/<Name>.prompt.md`
(worked examples) and `<Name>.d.ts` (exact props). Content and product copy are
Turkish — keep new copy Turkish to match.
