# design-sync notes — @alkaros/pos-terminal

Repo-specific gotchas for future syncs.

## Environment
- **Node/pnpm not on PATH.** Node 24.19.0 lives at `C:\Program Files\nodejs\node.exe`; add it to PATH for the session (`export PATH="/c/Program Files/nodejs:$PATH"`). pnpm is provisioned via corepack — set `COREPACK_ENABLE_STRICT=0` and use `corepack pnpm ...`.
- **Playwright** for the render check: the machine has a Python-installed playwright cache at `C:\Users\semih\AppData\Local\ms-playwright\` with **chromium build 1234** (Chrome 151). The node playwright that pins build 1234 is **`playwright@1.62.0`** — `npm i -D playwright@1.62.0` in `.ds-sync/` reuses the cache, no browser download.

## Design system shape
- **DS location:** `src/Clients/PosTerminal/src/design-system/` in a .NET monorepo. Package `@alkaros/pos-terminal` (pnpm@11.19.0, node >=24). 8 components, all in `primitives.tsx` + `Icon.tsx`: Button, TextField, SelectField, ValidationSummary, StateMessage, ModalDialog, ContextDrawer, Icon.
- **No library build ships.** `package.json` has no `module`/`main`/`exports`; the `build` script (`tsc --noEmit && vite build`) produces an *app* bundle, not a component entry.
  - **We build a minimal library dist ourselves:** `node .design-sync/build-ds-dist.mjs` (this is `cfg.buildCmd`) emits JS + `.d.ts` for `src/design-system/` plus a `package.json` (with `types`/`module`) into `src/Clients/PosTerminal/dist/ds/` (gitignored). The converter's `--entry` points at `dist/ds/index.js`; `cfg.*` paths are then relative to `dist/ds/` (hence `srcDir: "../../src/design-system"`, `tsconfig: "../../tsconfig.json"`).
  - Why a real dist and not synth-entry mode: the converter only triggers `deriveComponentsFromSrc` when `resolveDistEntry` returns null, which needs *no* `--entry` — but without `--entry` it can't locate `PKG_DIR` (no `node_modules/@alkaros/pos-terminal` self-link). A real built entry threads both needles and gives strong `.d.ts` contracts.
  - `build-ds-dist.mjs` ignores tsc's non-zero exit (CSS side-effect imports raise TS2882; the `.js`/`.d.ts` still emit) and asserts `index.d.ts` exists.
- **`src` enrichment is weak:** only `Icon` fuzzy-matches a source file (`Icon.tsx`); the other 7 live in `primitives.tsx` so they don't match `<Name>.tsx` and stay in group `general` with no JSDoc. `primitives.tsx` has no per-component doc comments anyway, so nothing is lost. Group `general` is fine.

## CSS / tokens / fonts
- **Do NOT set `cfg.cssEntry`.** esbuild already inlines the full chain into `_ds_bundle.css` via the JS imports: `primitives.tsx` → `primitives.css` (which `@import`s `tokens.css`) + `Icon.tsx` → `icon.css`. Setting `cssEntry` makes the converter *append the raw file* (with its unresolved `@import "./tokens.css"`) to `_ds_bundle.css` → `[CSS_IMPORT_MISSING]` blocking error.
- **`tokensGlob: "tokens.css"`** — tokens also live inlined in `_ds_bundle.css`; the glob is for the tokens/ display.
- **Inter font: wired from CDN, by user decision (2026-09-03).** The DS token `--ds-font-sans` leads with `Inter` but the repo ships zero font files (the app falls back to `system-ui` in prod). `build-ds-dist.mjs` emits `dist/ds/fonts.css` with 8 `@font-face` rules (weights 400/500/600/700 × latin + latin-ext subsets, `unicode-range`-split) pointing at `@fontsource/inter@5.1.1` woff2 on jsDelivr. `cfg.extraFonts: "fonts.css"` pulls them into the `styles.css` closure. **latin-ext is required** — carries the Turkish glyphs (ı ş ğ İ) the UI uses.

## Misc
- **DESIGN.md is aspirational, not shipped.** The repo-root `DESIGN.md` "Stitch" token spec (`--color-primary: #0D5257`, Slate palette) does NOT match the shipped `src/design-system/tokens.css` (`--ds-color-brand: #283a4a`). Code comments reference "deep-analysis finding F-3" reconciling the divergence. Sync `tokens.css`, ignore DESIGN.md's values.
- **Turkish UI strings** appear in some components (`ModalDialog` close `aria-label="Pencereyi kapat"`, `ContextDrawer` `"Bağlam panelini kapat"`). Expected.

## Overlay components need cardMode overrides
`ModalDialog` and `ContextDrawer` render `position: fixed` content (backdrop / sheet) that no grid cell can hold — `[GRID_OVERFLOW]`. `cfg.overrides` pins each to `{cardMode: "single", primaryStory, viewport}`.
- **ModalDialog viewport is 880×520 on purpose**: `primitives.css` has an `@media (max-width: 767px)` block that makes the dialog full-bleed (no radius, `align-items: stretch` → buttons balloon). A ≥768px viewport avoids it and shows the real desktop modal with its scrim.
- ContextDrawer viewport 440×460; the `Sheet` story wraps the fixed panel in a `transform: translateZ(0)` container so it's a containing block.

## Known render warns
- `[FONT_MISSING] "Inter"` — **accepted.** After the handoff `tokens.css`, `--ds-font-sans` is `Manrope, Inter, …`. `build-ds-dist.mjs` ships Manrope + DM Mono `@font-face` (Fontsource 5.3.0); Inter is only a fallback name and is deliberately not shipped. The pane renders Manrope.

## Gotchas hit this run
- **`rm -rf ds-bundle` fails EPERM on Windows while `http-serve.mjs` is running** (it holds a dir handle). Kill the serve before any `package-build.mjs` / `resync.mjs`: `Get-CimInstance Win32_Process -Filter "Name='node.exe'" | ? { $_.CommandLine -like '*http-serve*' } | % { Stop-Process $_.ProcessId -Force }`.
- **The driver verdict JSON prints `"shape": "storybook"`** even though this is the package shape — cosmetic bug in `resync.mjs`'s verdict header; the stages and `cfg.shape` are correct. Ignore it.
- **No `tokens/` folder** — `cfg.tokensGlob` was removed (it's a no-op without `cfg.tokensPkg`: `copyTokens()` returns early). Harmless: tokens are inlined in `_ds_bundle.css` (which `styles.css` imports), so the `styles.css` closure is complete. The README says "ships one compiled stylesheet rather than separate token files".
- **`.d.ts` extraction drops the native HTML-attribute intersections** (`ButtonHTMLAttributes`, `InputHTMLAttributes`, `SelectHTMLAttributes`) — the converter's `[DTS_STYLE_SYSTEM]` filter strips them. It also truncated `Icon`'s `name` union to 16/26 + `(string & {})`, and invented a phantom `children?` on `TextField`. **Fixed via `cfg.dtsPropsFor`** for Button / TextField / SelectField / Icon / ModalDialog — hand-curated prop bodies (meaningful native props + the real unions). Keep those in sync if the upstream component signatures change.

## Preview authoring rules learned here
- **Preview stories MUST be `export const Name = () => (...)`, NOT `export function Name()`.** Both render, but the converter's `previewExamples()` only harvests `export const` into the `.prompt.md` `## Examples` section. `export function` → every `.prompt.md` ships with zero usage examples. All 8 previews use the `const` arrow form.
- Keep each story self-contained (no module-level helpers referenced from inside a story) — the harvested snippet is sliced per `export const` and won't include anything above it. `Icon.tsx` `Gallery` defines its `names` array inside the story for this reason.
- Overlay stories (`ModalDialog.Open`, `ContextDrawer.Sheet`) wrap the fixed-position element in a `position:relative; transform:translateZ(0); overflow:hidden` box so it stays in-card; a short comment marks it as a preview device.

## Self-audit (2026-09-03, after first upload)
Deep audit ran a headless smoke test (all 8 exports load + render, 0 errors; Inter woff2 HTTP 200 from browser), verified the CSS closure carries tokens + component rules, and diffed local vs remote. Found + fixed: no `.prompt.md` examples (D1), 4 weak `.d.ts` (D2), dead `tokensGlob` config (D3, removed), redundant `label` on gallery icons (D4). Re-uploaded. `_adherence.oxlintrc.json` + `_ds_manifest.json` on the remote are **app-generated** by the self-check — not ours, never delete them; they're outside the plan's delete globs.

## Re-sync risks
- **`build-ds-dist.mjs` flags are hand-tuned**, not from the repo's tsconfig (which is `noEmit`). If `src/design-system/` gains new files, add them to the `tsc` arg list in `build-ds-dist.mjs`.
- **Inter `@font-face` URLs are pinned to `@fontsource/inter@5.1.1`** and its `unicode-range` values are copied inline. If Fontsource restructures paths, the font 404s silently (design pane falls back to system-ui). Re-verify the URLs resolve on a major re-sync.
- **`dist/ds/` is gitignored** — a fresh clone must run `cfg.buildCmd` before the converter.
- The 8 components are discovered from the emitted `.d.ts`; a new export in `primitives.tsx`/`Icon.tsx` flows through automatically once `build-ds-dist.mjs` recompiles.
