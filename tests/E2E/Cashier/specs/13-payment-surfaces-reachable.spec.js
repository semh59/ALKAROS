import { randomUUID } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { loginViaApi, createBill, readTenderSummary } from '../lib/paymentHelpers.js';

// V1-RMD-265: "registered and unit-tested" is not "reachable". These specs walk
// only what a user (or a manager's tooling) can actually reach from the running
// app: no hand-typed page URLs except the two HTTP management endpoints.
test.describe('Ödeme yüzeylerine gerçek erişim (V1-RMD-265)', () => {
  test('kasiyer ekranının başlığından Kasa Oturumu sayfasına gidilir', async ({ page }) => {
    const seed = readSeed();
    await loginViaApi(page, seed);
    await page.goto('/cashier/index.html');
    await page.locator('#cashSessionLink').click();
    await expect(page).toHaveURL(/payments\/cash-session\/index\.html/);
    await expect(page.getByRole('heading', { name: /Vardiyayı Başlat|Kasa Girişi|Zaten Açık/ })).toBeVisible({ timeout: 15_000 });
  });

  test('PosTerminal hesap ekranından "Tahsilata geç" ile Tahsilat sayfası açılır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    await page.evaluate((id) => localStorage.setItem('alkaros.current-bill-id', id), billId);

    await page.goto('/billing');
    await page.getByRole('link', { name: 'Tahsilata geç' }).click();
    await expect(page).toHaveURL((url) => url.pathname.endsWith('/split-payment/index.html') && url.searchParams.get('billId') === billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
  });

  test('ödeme mutabakat taraması gerçek çözülmemiş ödemeden vaka açar; ödeme raporu okunur', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    const tender = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'BankCard', Amount: 100, IdempotencyKey: randomUUID(), Note: null },
    });
    expect((await tender.json()).outcome).toBe('RequiresReconciliation');
    const paymentId = (await readTenderSummary(page, terminalId, billId)).unsettledPayment.paymentId;

    const scan = await page.request.post('/api/v1/management/payments/reconciliation-scan');
    expect(scan.ok(), `scan -> ${scan.status()}`).toBeTruthy();
    const results = await scan.json();
    expect(results).toHaveLength(7);
    expect(results.find((r) => r.sourceName === 'HuginUnknown').casesCreatedOrDeduplicated).toBeGreaterThanOrEqual(1);

    const cases = await (await page.request.get('/api/v1/management/reconciliation/cases?status=Open&limit=200')).json();
    expect(cases.some((c) => c.deduplicationKey === `hugin-unknown:${paymentId}`)).toBeTruthy();

    const today = new Date().toISOString().slice(0, 10);
    const report = await page.request.get(`/api/v1/management/payments/settlement-report?businessDate=${today}`);
    expect(report.ok(), `report -> ${report.status()}`).toBeTruthy();
    expect((await report.json()).netRefunds.blockedBy).toBe('V13-ALC-004');
  });
});
