import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// V1-RMD-297: the login response hands the waiter a bounded offline authority budget (the E2E role holds one
// bills.comp line: a single comp up to 300 TL, and no bills.void line). Offline, a comp within it is authorized
// locally and queued; a void outside it is refused and never queued; on reconnect the real
// /offline-reconciliation endpoint records the queued comp for manager review and its Turkish brief is shown.

async function readOfflineActions(page) {
  return page.evaluate(() => JSON.parse(localStorage.getItem('alkaros_waiter_offline_actions') || '[]'));
}

async function sendOneKofte(page, tableId) {
  await page.locator(`[data-table="${tableId}"]`).click();
  await expect(page.locator('#productList [data-product]').first()).toBeVisible();
  await page.locator(`[data-product="${seed.plainProductId}"]`).click();
  await expect(page.locator('#cartCount')).toHaveText('1');
  const submitted = page.waitForResponse((response) =>
    response.url().includes('/submit') && response.request().method() === 'POST');
  await page.locator('#btnSendFromMenu').click();
  expect((await submitted).ok()).toBeTruthy();
  await expect(page.locator('#tablesScreen')).toHaveAttribute('data-state', 'on');
  await page.locator(`[data-table="${tableId}"]`).click();
  await expect(page.locator('#billSheet')).toHaveClass(/is-open/);
}

test.describe('Çevrimdışı iptal/ikram bütçesi (V1-RMD-297)', () => {
  test('bütçe içindeki ikram çevrimdışı kuyruğa girer, bütçe dışı iptal reddedilir, bağlanınca uzlaştırılır', async ({ page, context }) => {
    await login(page, seed);
    const budget = await page.evaluate(() => JSON.parse(localStorage.getItem('alkaros_waiter_offline_budget') || 'null'));
    expect(budget).not.toBeNull();
    expect(budget.roleCode).toBe('e2e-waiter-role');
    expect(budget.lines.map((line) => line.permissionCode)).toEqual(['bills.comp']);

    await sendOneKofte(page, seed.offlineAuthorityTableIds[0]);
    const kofteLine = page.locator('.line', { hasText: 'E2E Köfte' });
    await expect(kofteLine.locator('[data-comp]')).toBeVisible();

    await context.setOffline(true);
    await expect(page.locator('#ribbonText')).toContainText('Bağlantı yok');

    // Outside the budget (no bills.void line): refused offline, nothing queued, never shown as approved.
    await kofteLine.locator('[data-void-sent]').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await page.locator('[data-reason]').first().click();
    await page.locator('#optionsConfirm').click();
    await expect(page.locator('.toast-text', { hasText: 'Bu işlem çevrimdışı yapılamaz. Bağlanınca tekrar deneyin.' }))
      .toBeVisible();
    expect(await readOfflineActions(page)).toHaveLength(0);
    await page.keyboard.press('Escape');
    await expect(page.locator('#optionsSheet')).not.toHaveClass(/is-open/);

    // Within the budget: authorized locally and queued under the online request's idempotency key.
    await kofteLine.locator('[data-comp]').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await page.locator('[data-reason]').first().click();
    await page.locator('#optionsConfirm').click();
    await expect(page.locator('.toast-text', { hasText: 'bağlanınca uzlaştırılacak' })).toBeVisible();
    const queued = await readOfflineActions(page);
    expect(queued).toHaveLength(1);
    expect(queued[0]).toMatchObject({
      permissionCode: 'bills.comp',
      requesterRoleCode: 'e2e-waiter-role',
      amount: 280,
      subjectType: 'OrderItem',
    });

    // The single comp the line allows is spent: a second offline comp is refused.
    await kofteLine.locator('[data-comp]').click();
    await page.locator('[data-reason]').first().click();
    await page.locator('#optionsConfirm').click();
    await expect(page.locator('.toast-text', { hasText: 'Çevrimdışı işlem hakkınız doldu' })).toBeVisible();
    expect(await readOfflineActions(page)).toHaveLength(1);
    await page.keyboard.press('Escape');

    // Reconnect: the real reconciliation endpoint records the comp for manager review.
    const reconciled = page.waitForResponse((response) =>
      response.url().includes('/offline-reconciliation') && response.request().method() === 'POST');
    await context.setOffline(false);
    const response = await reconciled;
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.results).toHaveLength(1);
    expect(body.results[0].idempotencyKey).toBe(queued[0].idempotencyKey);
    expect(body.results[0].status).toBe('Pending');
    await expect(page.locator('.toast-text', { hasText: body.summary.brief })).toBeVisible();
    expect(body.summary.brief).toContain('1 çevrimdışı işlem değerlendirildi');
    await expect.poll(() => readOfflineActions(page)).toHaveLength(0);
  });
});
