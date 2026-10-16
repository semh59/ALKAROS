// Screenshots of the management screens (errors, user journeys) in the Docker dev stack, logged in with the proof's test account.
// Usage: node screens.mjs <outDir> [explore]
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';

const ROOT = resolve(import.meta.dirname, '../..');
const require = createRequire(join(ROOT, 'tests', 'E2E', 'WaiterPwa', 'package.json'));
const { chromium } = require('playwright');
const OUT = process.argv[2] ?? import.meta.dirname;
const explore = process.argv[3] === 'explore';
const BASE = 'http://localhost:8090';

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'tr-TR' });
const page = await context.newPage();
await page.goto(BASE + '/');
await page.waitForTimeout(1500);
await page.getByLabel('Kullanıcı adı').fill('kanit.540');
await page.getByLabel('Parola').fill('Kanit540Sifre!2026');
await page.getByRole('button', { name: 'Giriş yap' }).click();
await page.waitForTimeout(3000);
if (explore) {
  console.log('URL', page.url());
  console.log((await page.locator('body').innerText()).slice(0, 800));
  console.log(await page.locator('input, button').evaluateAll((els) => els.map((e) => `${e.tagName}#${e.id}[${e.name || e.getAttribute('aria-label') || e.textContent?.trim().slice(0, 30)}]`).join(' | ')));
  await page.screenshot({ path: join(OUT, 'explore.png') });
}
if (!explore) {
  await page.getByText('Yönetim', { exact: true }).first().click();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: join(OUT, 'screen-0-management.png') });
  await page.getByRole('tab', { name: 'Hatalar' }).or(page.getByRole('button', { name: 'Hatalar' })).first().click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: join(OUT, 'screen-1-errors.png'), fullPage: true });
  await page.getByRole('tab', { name: 'Kullanıcı yolculuğu' }).or(page.getByRole('button', { name: 'Kullanıcı yolculuğu' })).first().click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: join(OUT, 'screen-2-journeys-requests.png'), fullPage: true });
  await page.getByRole('tab', { name: 'Düğmeler' }).or(page.getByRole('button', { name: 'Düğmeler' })).first().click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: join(OUT, 'screen-3-journeys-buttons.png'), fullPage: true });
  await page.getByRole('tab', { name: 'İstekler' }).or(page.getByRole('button', { name: 'İstekler' })).first().click();
  await page.waitForTimeout(2000);
  const row = page.locator('tr', { hasText: 'table-management/tables' }).first();
  await row.getByRole('button', { name: 'Zaman çizelgesi' }).click();
  await page.waitForTimeout(2500);
  await page.screenshot({ path: join(OUT, 'screen-4-timeline.png'), fullPage: true });
}
await browser.close();
