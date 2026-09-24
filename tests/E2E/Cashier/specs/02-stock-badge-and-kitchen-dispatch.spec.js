import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

test.describe('Kalan stok rozeti ve mutfağa gönderim (vanilla Cashier - /cashier)', () => {
  test('ürün sepete eklenir, kalan-stok rozeti doğru görünür, sipariş mutfağa gönderilir', async ({ page }) => {
    const seed = readSeed();
    // The vanilla Cashier client (src/Clients/Cashier) has no login screen
    // of its own - it reads the session cookie the PosTerminal SPA's real
    // login just set (see cashier-app.js's own bootstrapSession()), same
    // single-origin, same browser context this suite runs both under.
    await loginCashier(page, seed);
    // The exact file, not the bare "/cashier/" directory path: without
    // Caddy in front (this suite talks to the Host binary directly, see
    // README - Caddy is what resolves a directory request to its
    // index.html in production, via try_files + file_server), the Host's
    // own raw UseDefaultFiles/UseStaticFiles pipeline does not rewrite a
    // trailing-slash directory request to its index.html here; confirmed
    // with a real host boot + curl before writing this around it - every
    // subdirectory's own index.html serves fine by its exact path.
    await page.goto('/cashier/index.html');

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    const normalStockCard = page.locator(`.pos-product-card[data-product-id="${seed.normalStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });

    // Seeded on-hand quantity is 3 (< 5) - renderStockBadge's own is-low
    // threshold - versus 500 for the normal product.
    await expect(lowStockCard.locator('.product-stock.is-low')).toHaveText('Kalan 3');
    await expect(normalStockCard.locator('.product-stock')).not.toHaveClass(/is-low/);
    await expect(normalStockCard.locator('.product-stock')).toHaveText('Kalan 500');

    await lowStockCard.click();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');

    // V1-RMD-253 replaced the blocking native alert() with a self-dismissing
    // toast (role="status" on success); this spec used to wait for a
    // `dialog` event that no longer fires, so it timed out even though the
    // dispatch itself succeeded.
    await page.locator('#btnDispatchOrder').click();
    await expect(page.locator('#toastRegion .toast--success[role="status"]'))
      .toContainText('Sipariş mutfağa iletildi.');

    // A successful dispatch clears the ticket back to its empty state.
    await expect(page.locator('.ticket-empty')).toBeVisible();
  });
});
