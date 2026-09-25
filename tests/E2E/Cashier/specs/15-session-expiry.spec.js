import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment, selectMethod, setAmount } from '../lib/paymentHelpers.js';

// V1-RMD-291: a session dropped by the server (expiry, revocation) used to leave both static Kasa pages
// stuck on their already-rendered screen with only an inline "Oturumunuz sona erdi." message - no path
// back to a real login. A 401 from ANY call now sends the page to its login phase.
test.describe('Statik Kasa sayfaları: oturum düşünce giriş ekranına döner (V1-RMD-291)', () => {
  test('Hesap Ödeme: tahsilat sırasında 401 alınca giriş ekranına döner', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await selectMethod(page, 'Eft');
    await setAmount(page, '100');
    await page.locator('#eft-confirm').check().catch(() => {});
    await page.route('**/billing/bills/**/tenders/', (route) => {
      if (route.request().method() !== 'POST') return route.continue();
      return route.fulfill({ status: 401, contentType: 'application/json', body: '{}' });
    });
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();

    await expect(page.getByRole('heading', { name: 'Kasa Girişi' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('link', { name: 'Giriş ekranına dön' })).toHaveAttribute('href', '/');
  });

  test('Kasa Oturumu: vardiya açılışı sırasında 401 alınca gerçek giriş formuna döner', async ({ page }) => {
    const seed = readSeed();
    await loginViaApi(page, seed);
    await page.goto('/cashier/payments/cash-session/index.html');
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });

    await page.route('**/cash-sessions', (route) => {
      if (route.request().method() !== 'POST') return route.continue();
      return route.fulfill({ status: 401, contentType: 'application/json', body: '{}' });
    });
    await page.locator('#opening-balance').fill('100');
    await page.locator('#open-btn').click();

    await expect(page.getByRole('heading', { name: 'Kasa Girişi' })).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('#username')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
  });
});
