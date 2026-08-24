const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const prototypeRoot = path.resolve(__dirname, '..');
const html = fs.readFileSync(path.join(prototypeRoot, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(prototypeRoot, 'styles.css'), 'utf8');
const app = fs.readFileSync(path.join(prototypeRoot, 'app.js'), 'utf8');

test('viewport permits user zoom and exposes a keyboard skip link', () => {
  assert.match(html, /name="viewport" content="width=device-width, initial-scale=1\.0"/);
  assert.doesNotMatch(html, /user-scalable=no|maximum-scale=1/);
  assert.match(html, /class="skip-link" href="#app-viewport"/);
});

test('responsive contract covers desktop, tablet, phone, focus, and reduced motion', () => {
  for (const breakpoint of ['1180px', '900px', '760px', '520px']) {
    assert.match(css, new RegExp(`@media \\(max-width: ${breakpoint.replace('.', '\\.')}\\)`));
  }
  assert.match(css, /:focus-visible\s*\{/);
  assert.match(css, /@media \(prefers-reduced-motion: reduce\)/);
  assert.doesNotMatch(css, /outline:\s*none/);
  assert.doesNotMatch(css, /!important/);
});

test('dynamic table and product cards render as native buttons', () => {
  assert.match(app, /<button type="button" class="table-card" data-table-id=/);
  assert.match(app, /<button type="button" class="table-card" data-wtr-table-id=/);
  assert.match(app, /<button type="button" class="product-card[^`]+data-prod-id=/);
  assert.match(app, /<button type="button" class="product-card[^`]+data-wtr-prod-id=/);
});

test('dialogs receive names, focus trapping, Escape handling, and focus restoration', () => {
  assert.match(app, /function setupAccessibility\(\)/);
  assert.match(app, /dialog\.setAttribute\('aria-labelledby', heading\.id\)/);
  assert.match(app, /event\.key === 'Escape'/);
  assert.match(app, /event\.key !== 'Tab'/);
  assert.match(app, /returnFocus\.get\(dialog\)/);
  assert.match(app, /target\.focus\(\)/);
});
