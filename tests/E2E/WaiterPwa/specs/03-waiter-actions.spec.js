import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// Reuses the table 02-ordering.spec.js already opened and put an order on
// (table-transfer needs a real open order to move; transfer-server needs
// a real attributed order to hand off) - same shared-page-across-tests
// pattern, same reason.
test.describe.configure({ mode: 'serial' });

test.describe('Garson eylemleri (masa devri, garson devri, yardım, vardiya)', () => {
  let sharedPage;

  test('1. giriş yapılır ve dolu masa açılır', async ({ browser }) => {
    const context = await browser.newContext();
    sharedPage = await context.newPage();
    await login(sharedPage, seed);
    await sharedPage.locator(`[data-table="${seed.tableId}"]`).click();
    await expect(sharedPage.locator('#billSheet')).toHaveClass(/is-open/);
  });

  test('2. yardım çağrısı yöneticiye gider', async () => {
    await sharedPage.locator('#btnHelpRequest').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    await sharedPage.locator('[data-reason]').first().click();
    const helpResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/help-requests') && response.request().method() === 'POST');
    await sharedPage.locator('#optionsConfirm').click();
    const response = await helpResponse;
    expect(response.status()).toBe(200);
  });

  test('3. masa E2E-2ye taşınır', async () => {
    await sharedPage.locator('#btnMoveTable').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    await sharedPage.locator(`[data-target="${seed.secondTableId}"]`).click();
    const transferResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/table-management/transfers') && response.request().method() === 'POST');
    await sharedPage.locator('#optionsConfirm').click();
    const response = await transferResponse;
    expect(response.status()).toBe(200);

    // The table screen now shows E2E-2 as occupied and E2E-1 free again.
    await sharedPage.locator('#btnMenuBack').click();
    await expect(sharedPage.locator(`[data-table="${seed.secondTableId}"]`)).toContainText('Dolu');
    await expect(sharedPage.locator(`[data-table="${seed.tableId}"]`)).toContainText('Boş');
  });

  test('4. vardiya özetim okunabilir', async () => {
    await sharedPage.locator('#btnProfile').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    const summaryResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/my-shift-summary'));
    await sharedPage.locator('[data-profile="shift-summary"]').click();
    const response = await summaryResponse;
    expect(response.status()).toBe(200);
    await expect(sharedPage.locator('#optionsTitle')).toHaveText('Vardiya özetim');
    await sharedPage.locator('#optionsClose').click();
  });

  test('5. açık masalar meslektaşa devredilir', async () => {
    await sharedPage.locator('#btnProfile').click();
    const staffResponse = sharedPage.waitForResponse((response) => response.url().includes('/orders/staff'));
    await sharedPage.locator('[data-profile="transfer-server"]').click();
    await staffResponse;
    await expect(sharedPage.locator('#optionsBody')).toContainText(seed.colleagueDisplayName);

    await sharedPage.locator('#optionsBody [data-target]').first().click();
    await sharedPage.locator('#handoffNoteInput').fill('E2E devir notu');
    const transferServerResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/orders/transfer-server') && response.request().method() === 'POST');
    await sharedPage.locator('#optionsConfirm').click();
    const response = await transferServerResponse;
    expect(response.status()).toBe(200);
  });
});
