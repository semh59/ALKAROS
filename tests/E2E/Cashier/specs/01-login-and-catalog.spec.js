import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

test.describe('Kasiyer girişi ve katalog', () => {
  test('kasiyer giriş yapar, Kasa ekranı açılır ve katalog yüklenir', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);

    // Real backend data, not a stub - the two seeded products must both
    // actually render in the product grid.
    await expect(page.getByRole('button', { name: /E2E Kasa Düşük Stok/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /E2E Kasa Normal Stok/ })).toBeVisible();

    // A real, live /health-backed status pill, not a static label.
    await expect(page.locator('.system-state.compact.online')).toHaveCount(1);
  });
});
