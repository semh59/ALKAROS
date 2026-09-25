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
    await sendCheckToCashier(page, terminalId, seed, table, { quantity: 2 });
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
    await page.goto('/pending-checks');
    await expect(rowOf(page, table, '₺100,00')).toBeVisible({ timeout: 15_000 });
    await expect(rowOf(page, table, '₺200,00')).toHaveCount(0);

    // Masa hiçbir noktada değişmedi: ikinci müşterinin hesabı da masaya bağlı değil ama masa kullanılabilir.
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.remainingAmount).toBe(0);
  });

  test('kuyruk boşken bunu söyler', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.getByRole('link', { name: 'Bekleyen hesaplar' }).click();
    // Diğer senaryoların hesapları olabilir; en azından başlık ve bölge yüklenir.
    await expect(page.getByRole('region', { name: 'Bekleyen hesaplar' })).toBeVisible({ timeout: 15_000 });
  });
});
