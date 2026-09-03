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

// The design system's --ds-font-sans leads with "Inter" but the repo ships no
// font files (the app falls back to system-ui in prod). For the Claude Design
// pane we want the intended look, so wire Inter from the Fontsource CDN via
// cfg.extraFonts -> fonts/fonts.css in the styles.css closure. Inter is SIL OFL.
const FONTSOURCE = 'https://cdn.jsdelivr.net/npm/@fontsource/inter@5.1.1/files';
// Two subsets: latin-ext carries the Turkish glyphs (ı ş ğ İ) the UI needs.
const SUBSETS = {
  latin:
    'U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+2074,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD',
  'latin-ext':
    'U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF',
};
const interFace = (w, sub) => `@font-face {
  font-family: 'Inter';
  font-style: normal;
  font-weight: ${w};
  font-display: swap;
  src: url('${FONTSOURCE}/inter-${sub}-${w}-normal.woff2') format('woff2');
  unicode-range: ${SUBSETS[sub]};
}`;
writeFileSync(
  join(OUT, 'fonts.css'),
  [400, 500, 600, 700].flatMap((w) => Object.keys(SUBSETS).map((s) => interFace(w, s))).join('\n\n') + '\n',
);

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
