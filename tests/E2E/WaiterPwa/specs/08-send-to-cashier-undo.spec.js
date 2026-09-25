import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// V1-RMD-281: "Hesabi kasaya gonder" is one tap, so it will be tapped by mistake. Until now there was no way
// back and no browser test of the send flow at all. A check sent by mistake returns to its table with one tap
// while no money has moved; it is refused, in Turkish, when a newer check already sits on the table.
async function openTableWithOneItem(page, tableId) {
  await page.locator(`[data-table="${tableId}"]`).click();
  await expect(page.locator('#productList [data-product]').first()).toBeVisible();
  await page.locator(`[data-product="${seed.plainProductId}"]`).click();
  await expect(page.locator('#cartCount')).toHaveText('1');
  await page.locator('#btnSendFromMenu').click();
}

async function sendCheckToCashier(page, tableId) {
  await page.locator(`[data-table="${tableId}"]`).click();
  await expect(page.locator('#billSheet')).toHaveClass(/is-open/);
  await page.locator('#btnSendToCashier').click();
  await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
  await page.locator('#optionsConfirm').click();
  await expect(page.locator('.toast-text', { hasText: 'kasaya gönderildi' })).toBeVisible();
}

/** Server truth: the check the table is pointing at right now (null = the table is free for the next party). */
async function tableCheck(page, tableId) {
  const session = await (await page.request.get('/api/v1/auth/session/current')).json();
  const body = await (await page.request.get(`/api/v1/terminals/${session.terminalId}/table-management/tables`)).json();
  const rows = Array.isArray(body) ? body : body.tables || body.items || [];
  const row = rows.find((table) => table.tableId === tableId);
  return { orderId: row.currentOrderId, status: row.status };
}

test.describe('Hesabı kasaya gönder ve geri al (V1-RMD-281)', () => {
  test('yanlışlıkla gönderilen hesap "Geri al" ile masaya döner', async ({ page }) => {
    await login(page, seed);
    const tableId = seed.sendTableIds[0];
    await openTableWithOneItem(page, tableId);
    await page.locator('#btnMenuBack').click();
    await sendCheckToCashier(page, tableId);

    // Gönderim: masa boşaldı, hesap masada görünmüyor.
    await expect(page.locator(`[data-table="${tableId}"]`)).not.toContainText('Dolu');
    expect((await tableCheck(page, tableId)).orderId).toBeNull();

    // Geri al: hesap ve kalemi masaya döner, masa yeniden dolu.
    await page.locator('.toast-undo').click();
    await expect(page.locator('.toast-text', { hasText: 'geri alındı' })).toBeVisible();
    await expect(page.locator(`[data-table="${tableId}"]`)).toContainText('Dolu');
    await expect.poll(async () => (await tableCheck(page, tableId)).status, { timeout: 10_000 }).toBe('Occupied');
    expect((await tableCheck(page, tableId)).orderId).toBeTruthy();
  });

  test('masada yeni bir hesap açılmışsa geri alma reddedilir ve nedeni Türkçe söylenir', async ({ page }) => {
    await login(page, seed);
    const tableId = seed.sendTableIds[1];
    await openTableWithOneItem(page, tableId);
    await page.locator('#btnMenuBack').click();
    await sendCheckToCashier(page, tableId);

    // Yeni müşteri oturdu ve sipariş verildi (aynı masada yeni hesap).
    const session = await (await page.request.get('/api/v1/auth/session/current')).json();
    const base = `/api/v1/terminals/${session.terminalId}`;
    const draft = await (await page.request.post(`${base}/orders/table-draft`, {
      data: {
        id: crypto.randomUUID(), tableId, tableNumber: 'SND-2',
        items: [{ id: crypto.randomUUID(), productId: seed.plainProductId, name: 'x', productName: 'x', quantity: 1, unitPrice: 10, specialInstructions: null }],
      },
    })).json();
    expect(draft.orderId).toBeTruthy();

    await page.locator('.toast-undo').click();
    await expect(page.locator('.toast-text', { hasText: 'yeni bir hesap açık' })).toBeVisible();
  });
});
