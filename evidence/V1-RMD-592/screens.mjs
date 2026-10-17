// Docker dev stack proof for the printer form and the stale health marker (web proxy on :8090).
// Usage: node screens.mjs <outDir>   (test account is created in the dev database, fixture style, and deactivated at the end)
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { appendFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { randomUUID } from 'node:crypto';

const ROOT = resolve(import.meta.dirname, '../..');
const OUT = process.argv[2] ?? import.meta.dirname;
const require = createRequire(join(ROOT, 'tests', 'E2E', 'WaiterPwa', 'package.json'));
const { chromium } = require('playwright');
const { hashPassword } = await import(pathToFileURL(join(ROOT, 'tests', 'E2E', 'WaiterPwa', 'lib', 'passwordHash.js')).href);
const BASE = 'http://localhost:8090';
const USERNAME = 'kanit.592';
const PASSWORD = 'Kanit592Sifre!2026';
const LOG = join(OUT, 'screens.log');
writeFileSync(LOG, '');
const log = (step, data) => { const line = JSON.stringify({ step, ...data }); appendFileSync(LOG, line + '\n'); console.log(line); };
const psql = (sql) => execFileSync('docker', ['exec', '-i', 'alkaros-postgres-1', 'psql', '-U', 'alkaros', '-d', 'alkaros', '-v', 'ON_ERROR_STOP=1', '-tA', '-F', '|'], { input: sql, encoding: 'utf8' }).trim();

if (psql(`select count(*) from identity.users where username='${USERNAME}'`) === '0') {
  const userId = randomUUID();
  psql(`insert into identity.users (user_id, username, password_hash, display_name, active) values ('${userId}', '${USERNAME}', '${hashPassword(PASSWORD)}', 'Kanit 592', true);
        insert into identity.user_roles (user_role_id, user_id, role_id) select gen_random_uuid(), '${userId}', role_id from identity.roles where code='manager';`);
}
psql(`update identity.users set active = true where username='${USERNAME}'`);
psql("delete from kitchen.printers where name like 'Kanit592%'");

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1500, height: 1000 }, locale: 'tr-TR' });
const page = await context.newPage();
const posts = [];
page.on('response', async (response) => {
  const url = response.url();
  if (/\/kitchen-operations\/printers\//.test(url) && response.request().method() !== 'GET') {
    posts.push({ method: response.request().method(), status: response.status(), body: response.request().postData() });
  }
});
await page.goto(BASE + '/');
await page.waitForTimeout(1500);
await page.getByLabel('Kullanıcı adı').fill(USERNAME);
await page.getByLabel('Parola').fill(PASSWORD);
await page.getByRole('button', { name: 'Giriş yap' }).click();
await page.waitForTimeout(3000);
log('login', { url: page.url() });

await page.goto(BASE + '/kitchen');
await page.waitForTimeout(3000);
log('kitchen', { url: page.url(), hasAddButton: await page.getByRole('button', { name: 'Yazıcı ekle' }).count() });
await page.screenshot({ path: join(OUT, 'screen-0-kitchen.png'), fullPage: true });

// 1) a public address is refused with a Turkish sentence
await page.getByRole('button', { name: 'Yazıcı ekle' }).click();
await page.getByLabel('Yazıcı adı').fill('Kanit592 yanlis');
await page.getByLabel('İstasyon').fill('bar');
await page.getByLabel('Ağ adresi (IP)').fill('8.8.8.8');
await page.getByRole('button', { name: 'Kaydet' }).click();
await page.waitForTimeout(1500);
const refused = await page.locator('.kitchen-route-form__feedback').first().innerText().catch(() => '');
log('public-address', { feedback: refused, rowsInDatabase: psql("select count(*) from kitchen.printers where name='Kanit592 yanlis'") });
await page.screenshot({ path: join(OUT, 'screen-1-refused.png'), fullPage: true });

// 2) a valid printer is saved with brand and shows in the list
await page.getByLabel('Yazıcı adı').fill('Kanit592 bar yazicisi');
await page.getByLabel('Ağ adresi (IP)').fill('192.168.1.77');
await page.getByLabel('Yazıcı markası (Türkçe karakterler için)').selectOption('Star');
await page.getByRole('button', { name: 'Kaydet' }).click();
await page.waitForTimeout(2500);
log('saved', {
  requests: posts,
  row: psql("select name || '|' || station_id || '|' || ip_address || '|' || port || '|' || coalesce(profile,'-') || '|' || is_active from kitchen.printers where name like 'Kanit592%'"),
  listedOnScreen: (await page.locator('.kitchen-printer-list li').allInnerTexts()).filter((t) => t.includes('Kanit592')),
});
await page.screenshot({ path: join(OUT, 'screen-2-saved.png'), fullPage: true });

// 3) edit: switch the brand back to the server default
await page.locator('.kitchen-printer-list li', { hasText: 'Kanit592' }).getByRole('button', { name: 'Düzenle' }).click();
await page.getByLabel('Yazıcı markası (Türkçe karakterler için)').selectOption('Default');
await page.getByRole('button', { name: 'Kaydet' }).click();
await page.waitForTimeout(2500);
log('edited', { row: psql("select coalesce(profile,'-') from kitchen.printers where name like 'Kanit592%'"), requests: posts.slice(-1) });

// 4) stale health marker: the screen is shown an old measurement
await page.route('**/operations/health/latest', async (route) => {
  const response = await route.fetch();
  const body = await response.json().catch(() => null);
  await route.fulfill({ response, json: body ? { ...body, capturedAt: new Date(Date.now() - 10 * 60_000).toISOString() } : null });
});
await page.goto(BASE + '/kitchen');
await page.waitForTimeout(3000);
log('stale-marker', { onScreen: await page.getByText('Sağlık ölçümü eski').count(), text: (await page.getByText('Son ölçüm').first().innerText().catch(() => '')) });
await page.screenshot({ path: join(OUT, 'screen-3-stale.png'), fullPage: true });

await browser.close();
psql("delete from kitchen.printers where name like 'Kanit592%'");
psql(`update identity.users set active = false where username='${USERNAME}'`);
log('cleanup', { printersLeft: psql("select count(*) from kitchen.printers where name like 'Kanit592%'") });
