import { randomUUID } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment, readTenderSummary } from '../lib/paymentHelpers.js';

// V1-RMD-264: an unconfirmed card payment locks its bill (no real terminal
// exists to settle it). Only a user holding reconciliation.manage may declare
// "the card was NOT charged"; a plain cashier is told so, and nothing changes.
async function makeUnsettledBill(page, seed) {
  const terminalId = await loginViaApi(page, seed);
  const billId = await createBill(page, terminalId, seed, { quantity: 1 });
  const tender = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
    data: { Method: 'BankCard', Amount: 100, IdempotencyKey: randomUUID(), Note: null },
  });
  expect((await tender.json()).outcome).toBe('RequiresReconciliation');
  return billId;
}

async function currentTerminalId(page) {
  return (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
}

test.describe('Kart ödemesinin elle çözümü (V1-RMD-264)', () => {
  test('yetkisiz kasiyer çözemez; müdür "kart çekilmedi" der ve hesap kilidi kalkar', async ({ page }) => {
    const seed = readSeed();
    const billId = await makeUnsettledBill(page, seed);

    // Yetkisiz kasiyer: Türkçe uyarı görür, kilit ve ödeme olduğu gibi kalır.
    await loginViaApi(page, seed, seed.limitedUsername);
    await openSplitPayment(page, billId);
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 15_000 });
    await page.getByPlaceholder('Gerekçe (zorunlu)').fill('Kasiyer kendi başına çözmeye çalışıyor');
    await page.getByRole('button', { name: 'Kart çekilmedi olarak çöz' }).click();
    await expect(page.getByText('Bu işlem için müdür yetkisi gerekir.')).toBeVisible();
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible();
    let terminalId = await currentTerminalId(page);
    expect((await readTenderSummary(page, terminalId, billId)).unsettledPayment).toBeTruthy();

    // Müdür: gerekçe olmadan olmaz, gerekçeyle çözülür.
    await loginViaApi(page, seed);
    await openSplitPayment(page, billId);
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Kart çekilmedi olarak çöz' }).click();
    await expect(page.getByText('Gerekçe yazmalısınız.')).toBeVisible();
    expect((await readTenderSummary(page, await currentTerminalId(page), billId)).unsettledPayment).toBeTruthy();

    await page.getByPlaceholder('Gerekçe (zorunlu)').fill('Müşterinin kartı çekilmedi, slip yok');
    await page.getByRole('button', { name: 'Kart çekilmedi olarak çöz' }).click();
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeHidden({ timeout: 10_000 });
    await expect(page.getByText('Ödeme yöntemi')).toBeVisible();

    terminalId = await currentTerminalId(page);
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.unsettledPayment).toBeNull();
    // Para kaydı oluşmadı: hiçbir tahsis yok, kalan tutar aynı.
    expect(summary.allocations).toHaveLength(0);
    expect(summary.remainingAmount).toBe(100);
  });
});
