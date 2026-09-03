// Builds a minimal library dist for @alkaros/pos-terminal's design system so
// design-sync's converter has a real entry + .d.ts tree to read.
//
// The package itself ships no library build (its `build` script produces an
// app bundle), so we emit one here: JS + declarations for src/design-system/,
// plus a package.json with `types`/`module`, into dist/ds/ (gitignored).
//
// Run from the repo root:  node .design-sync/build-ds-dist.mjs
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const PKG = 'src/Clients/PosTerminal';
const OUT = join(PKG, 'dist/ds');
const tsc = join(PKG, 'node_modules/typescript/lib/tsc.js');

rmSync(OUT, { recursive: true, force: true });
mkdirSync(OUT, { recursive: true });

// CSS side-effect imports make tsc report TS2882 and exit non-zero even with
// --noEmitOnError false; the .js/.d.ts are still emitted. Ignore the status,
// then assert the entry declaration exists.
spawnSync(process.execPath, [
  tsc, '--ignoreConfig',
  join(PKG, 'src/design-system/index.ts'),
  join(PKG, 'src/design-system/primitives.tsx'),
  join(PKG, 'src/design-system/Icon.tsx'),
  '--declaration', '--outDir', OUT,
  '--jsx', 'react-jsx', '--module', 'esnext', '--moduleResolution', 'bundler',
  '--target', 'es2022', '--lib', 'es2023,dom,dom.iterable',
  '--skipLibCheck', '--esModuleInterop', '--strict', '--noEmitOnError', 'false',
], { stdio: 'inherit' });
if (!existsSync(join(OUT, 'index.d.ts'))) {
  console.error('tsc did not emit index.d.ts — aborting');
  process.exit(1);
}

for (const f of ['tokens.css', 'primitives.css', 'icon.css']) {
  copyFileSync(join(PKG, 'src/design-system', f), join(OUT, f));
}

// --ds-font-sans now leads with Manrope and --ds-font-mono is "DM Mono"
// (handoff design_handoff_alkaros_v1). The repo ships no font files, so for the
// Claude Design pane we wire both from the Fontsource CDN via
// cfg.extraFonts -> fonts/fonts.css in the styles.css closure. Manrope is
// SIL OFL, DM Mono is Apache-2.0. Inter stays only as a CSS fallback name and
// is not shipped.
// Two subsets: latin-ext carries the Turkish glyphs (ı ş ğ İ) the UI needs.
const SUBSETS = {
  latin:
    'U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+2074,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD',
  'latin-ext':
    'U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF',
};
const FS_VER = '5.3.0'; // pinned @fontsource version for both families
const face = (family, pkg, w, sub) => `@font-face {
  font-family: '${family}';
  font-style: normal;
  font-weight: ${w};
  font-display: swap;
  src: url('https://cdn.jsdelivr.net/npm/@fontsource/${pkg}@${FS_VER}/files/${pkg}-${sub}-${w}-normal.woff2') format('woff2');
  unicode-range: ${SUBSETS[sub]};
}`;
const rules = [
  ...[400, 500, 600, 700, 800].flatMap((w) =>
    Object.keys(SUBSETS).map((s) => face('Manrope', 'manrope', w, s))),
  ...[400, 500].flatMap((w) =>
    Object.keys(SUBSETS).map((s) => face('DM Mono', 'dm-mono', w, s))),
];
writeFileSync(join(OUT, 'fonts.css'), rules.join('\n\n') + '\n');

writeFileSync(join(OUT, 'package.json'), JSON.stringify({
  name: '@alkaros/pos-terminal',
  version: '1.0.0',
  type: 'module',
  module: './index.js',
  main: './index.js',
  types: './index.d.ts',
  exports: { '.': { types: './index.d.ts', import: './index.js' } },
}, null, 2) + '\n');

console.log('wrote', OUT);
