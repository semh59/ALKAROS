import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment } from '../lib/paymentHelpers.js';

test.describe('Kasa üst çubuğu ve ödeme sayfalarından dönüş (V1-RMD-784)', () => {
  for (const width of [768, 929, 1024, 1440]) {
    test(`üst çubuktaki her eylem ${width} px genişlikte pencere içinde ve tıklanabilir`, async ({ page }) => {
      await page.setViewportSize({ width, height: 800 });
      await loginCashier(page, readSeed());

      const outside = await page.locator('.pos-header-actions > *').evaluateAll(
        (items, viewportWidth) => items
          .map((item) => ({ name: item.textContent.trim(), box: item.getBoundingClientRect() }))
          .filter(({ box }) => box.width > 0 && (box.left < 0 || box.right > viewportWidth + 0.5))
          .map(({ name }) => name),
        width,
      );
      expect(outside).toEqual([]);

      await page.getByRole('button', { name: 'Oturumu kapat' }).scrollIntoViewIfNeeded();
      await expect(page.getByRole('button', { name: 'Oturumu kapat' })).toBeInViewport();
    });
  }

  test('ödeme sayfasından "Kasaya dön" ana kasaya götürür', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await page.getByRole('link', { name: '← Kasaya dön' }).click();
    await expect(page).toHaveURL((url) => url.pathname === '/');
  });

  test('kasa oturumu sayfasından "Kasaya dön" hızlı kasaya götürür', async ({ page }) => {
    await loginViaApi(page, readSeed());
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.getByRole('link', { name: '← Kasaya dön' }).click();
    await expect(page).toHaveURL(/\/cashier\/index\.html$/);
  });
});
