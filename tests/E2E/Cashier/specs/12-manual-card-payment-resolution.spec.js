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

  test('"kart çekildi": bir müdür fiş numarasıyla bildirir, KENDİSİ onaylayamaz; ikinci müdür onaylayınca hesap ödenir', async ({ page }) => {
    const seed = readSeed();
    const billId = await makeUnsettledBill(page, seed);

    // Birinci müdür fiş numarasıyla bildirir: para hareketi YOK, bildirim onay bekler.
    await openSplitPayment(page, billId);
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 15_000 });
    await page.getByPlaceholder('Fiş numarası').fill('E2E-FIS-7001');
    await page.getByRole('button', { name: 'Kart çekildi: onaya gönder' }).click();
    await expect(page.getByText('Onay bekliyor: fiş E2E-FIS-7001')).toBeVisible();
    await expect(page.getByText('ikinci bir yetkilinin onaylaması gerekir')).toBeVisible();
    // Kendi bildirimini onaylayamaz: onay düğmesi yok, yalnız geri çekme var.
    await expect(page.getByRole('button', { name: /Onayla: kart çekildi/ })).toHaveCount(0);
    let terminalId = await currentTerminalId(page);
    let summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(0);
    expect(summary.unsettledPayment.pendingConfirmation.slipNumber).toBe('E2E-FIS-7001');

    // İkinci müdür farklı bir oturumla onaylar: ödeme onaylanır, hesap kapanır.
    await loginViaApi(page, seed, seed.secondManagerUsername);
    await openSplitPayment(page, billId);
    await expect(page.getByText('Onay bekliyor: fiş E2E-FIS-7001')).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: 'Onayla: kart çekildi' }).click();
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeHidden({ timeout: 10_000 });

    terminalId = await currentTerminalId(page);
    summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(100);
    expect(summary.remainingAmount).toBe(0);
    expect(summary.unsettledPayment).toBeNull();

    // Ekstre eşleştirmesi için yönetici listesi fişi, tutarı ve iki kişiyi taşır.
    const list = await (await page.request.get('/api/v1/management/payments/manual-confirmations?status=Approved')).json();
    const entry = list.find((item) => item.slipNumber === 'E2E-FIS-7001');
    expect(entry).toBeTruthy();
    expect(entry.amount).toBe(100);
    expect(entry.decidedBy).not.toBe(entry.requestedBy);
  });
});
