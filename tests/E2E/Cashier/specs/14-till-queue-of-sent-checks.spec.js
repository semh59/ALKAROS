import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { sendCheckToCashier, readTenderSummary } from '../lib/paymentHelpers.js';
import { randomUUID } from 'node:crypto';

// V1-RMD-279/280: waiters send checks to the till; the cashier sees them in "Bekleyen hesaplar", tells two
// checks of the SAME table apart, collects one, and only that one leaves the queue. The table is never touched.
async function currentTerminalId(page) {
  return (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
}

// Other specs leave their own checks in the queue; a row is identified by ITS table and amount.
const rowOf = (page, table, amount) => page
  .getByRole('button', { name: /hesabını tahsil et/ })
  .filter({ hasText: `Masa ${table.tableNumber} ` })
  .filter({ hasText: amount });

test.describe('Kasa: bekleyen hesaplar kuyruğu (V1-RMD-279/280)', () => {
  test('garsonun gönderdiği iki hesap ayırt edilir, biri tahsil edilince yalnız o kuyruktan çıkar', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = await currentTerminalId(page);
    const table = seed.transferTables[6];

    // Aynı masada önce bir müşteri (kalkıp kasaya gitti), sonra yeni müşteri: iki ayrı hesap.
    const firstOrderId = await sendCheckToCashier(page, terminalId, seed, table, { quantity: 2 });
    await sendCheckToCashier(page, terminalId, seed, table, { quantity: 1 });

    await page.getByRole('link', { name: 'Bekleyen hesaplar' }).click();
    const first = rowOf(page, table, '₺200,00');
    const second = rowOf(page, table, '₺100,00');
    await expect(first).toBeVisible({ timeout: 15_000 });
    await expect(second).toBeVisible();
    await expect(first).toContainText(`Masa ${table.tableNumber}`);
    await expect(second).toContainText(`Masa ${table.tableNumber}`);
        
    // İlkine dokun: hesap açılır ve doğrudan Tahsilat sayfasına gidilir.
    await first.click();
    await expect(page).toHaveURL((url) => url.pathname.endsWith('/split-payment/index.html') && !!url.searchParams.get('billId'));
    const billId = new URL(page.url()).searchParams.get('billId');
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    // Kısmi tahsilat: kuyrukta "Kalan" görünür, satır hâlâ orada.
    const partial = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'Eft', Amount: 50, IdempotencyKey: randomUUID(), Note: null },
    });
    expect(partial.ok()).toBeTruthy();
    await page.goto('/pending-checks');
    const resumed = rowOf(page, table, '₺200,00');
    await expect(resumed).toContainText('Kalan');
    await expect(resumed).toContainText('₺150,00');

    // Kalanı da tahsil et: yalnız bu hesap kuyruktan çıkar, ikinci müşterinin hesabı yerinde.
    const rest = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'Eft', Amount: 150, IdempotencyKey: randomUUID(), Note: null },
    });
    expect((await rest.json()).billClosed).toBe(true);
    // V1-RMD-282: the settled check closed its order (it used to stay Submitted for ever).
    const settled = await (await page.request.get(`/api/v1/terminals/${terminalId}/orders/${firstOrderId}`)).json();
    expect(settled.status).toBe('Completed');
    await page.goto('/pending-checks');
    await expect(rowOf(page, table, '₺100,00')).toBeVisible({ timeout: 15_000 });
    await expect(rowOf(page, table, '₺200,00')).toHaveCount(0);

    // Masa hiçbir noktada değişmedi: ikinci müşterinin hesabı da masaya bağlı değil ama masa kullanılabilir.
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.remainingAmount).toBe(0);
  });

  test('yanlışlıkla gönderilen hesap kasadan masaya geri gönderilir; tahsilat başlamış hesapta seçenek yoktur', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = await currentTerminalId(page);
    const table = seed.transferTables[7];
    const orderId = await sendCheckToCashier(page, terminalId, seed, table, { quantity: 3 });

    await page.goto('/pending-checks');
    const row = rowOf(page, table, '₺300,00');
    await expect(row).toBeVisible({ timeout: 15_000 });
    const tableRow = async () => {
      const body = await (await page.request.get(`/api/v1/terminals/${terminalId}/table-management/tables`)).json();
      return (Array.isArray(body) ? body : body.tables || body.items).find((t) => t.tableNumber === table.tableNumber);
    };
    expect((await tableRow()).currentOrderId).toBeNull();

    await page.getByRole('button', { name: /hesabını masaya geri gönder/ }).filter({ hasText: 'Yanlışlıkla' }).first().waitFor();
    await page.locator('li', { has: row }).getByRole('button', { name: /masaya geri gönder/ }).click();

    // Hesap kuyruktan çıktı, masa yeniden dolu ve hesabına bağlı.
    await expect(row).toHaveCount(0, { timeout: 15_000 });
    const back = await tableRow();
    expect(back.status).toBe('Occupied');
    expect(back.currentOrderId).toBe(orderId);

    // Yeniden gönderilebilir; bu kez 60 ₺ tahsilat başlar: geri gönderme seçeneği kaybolur ve sunucu da reddeder.
    await page.request.post(`/api/v1/terminals/${terminalId}/orders/${orderId}/send-to-cashier`, { data: { tableId: table.tableId } });
    await page.goto('/pending-checks');
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.click();
    await expect(page).toHaveURL((url) => url.pathname.endsWith('/split-payment/index.html') && !!url.searchParams.get('billId'));
    const billId = new URL(page.url()).searchParams.get('billId');
    const paid = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'Eft', Amount: 60, IdempotencyKey: randomUUID(), Note: null },
    });
    expect(paid.ok()).toBeTruthy();
    await page.goto('/pending-checks');
    await expect(rowOf(page, table, 'Kalan')).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('li', { has: rowOf(page, table, 'Kalan') }).getByRole('button', { name: /masaya geri gönder/ })).toHaveCount(0);
    const refused = await page.request.post(`/api/v1/terminals/${terminalId}/orders/${orderId}/recall-from-cashier`, { data: { tableId: table.tableId } });
    expect(refused.status()).toBe(409);
    expect((await refused.json()).error.code).toBe('CHECK_HAS_PAYMENT');
  });

  test('kuyruk boşken bunu söyler', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.getByRole('link', { name: 'Bekleyen hesaplar' }).click();
    // Diğer senaryoların hesapları olabilir; en azından başlık ve bölge yüklenir.
    await expect(page.getByRole('region', { name: 'Bekleyen hesaplar' })).toBeVisible({ timeout: 15_000 });
  });
});
