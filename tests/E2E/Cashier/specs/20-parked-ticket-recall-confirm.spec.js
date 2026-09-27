import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-355 (independent 2026-09-26 audit, a low-severity finding): recallParkedTicket() used to call the
// native, unstylable browser confirm() when the current cart was non-empty. Replaced with an in-app modal
// (#confirmModal) - this suite proves it: a native confirm() is invisible to page.locator() and, with no
// page.on('dialog') handler registered here, Playwright auto-dismisses it, so if the OLD code were still in
// place this whole test would either see the cart silently NOT replaced (auto-dismiss = cancel) or hang; the
// NEW modal is a normal, queryable, clickable DOM element.
test.describe('Bekletilen fiş geri yükleme onayı (vanilla Cashier - /cashier)', () => {
  test('mevcut sepet doluyken geri yükleme, native confirm() değil uygulama içi modal ile onaylanır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    const normalStockCard = page.locator(`.pos-product-card[data-product-id="${seed.normalStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });

    // Park a ticket holding the first product.
    await lowStockCard.click();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');
    await page.locator('#btnParkTicket').click();
    await expect(page.locator('.ticket-empty')).toBeVisible();
    await expect(page.locator('#parkCountBadge')).toHaveText('1');

    // Fill the cart again with a different product before recalling.
    await normalStockCard.click();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Normal Stok');

    await page.locator('#btnRecallTicket').click();
    await expect(page.locator('#parkedModal')).toBeVisible();
    await page.locator('[data-action="recall"]').click();

    // The in-app modal, not a native dialog: queryable, has the exact Turkish
    // copy the old confirm() used, and role="alertdialog" for a screen reader.
    const confirmModal = page.locator('#confirmModal');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toHaveAttribute('role', 'alertdialog');
    await expect(page.locator('#confirmModalMessage')).toContainText(
      'Mevcut sepette ürünler var. Bekletilen fişi yüklemek mevcut sepeti değiştirecektir.'
    );

    // "Vazgeç": the cart must NOT be replaced, only the confirm modal closes - the parked-list modal
    // underneath stays open (cancelling a recall lets the cashier pick a different parked ticket next).
    await page.getByRole('button', { name: 'Vazgeç' }).click();
    await expect(confirmModal).toBeHidden();
    await expect(page.locator('#parkedModal')).toBeVisible();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Normal Stok');

    // Try again and accept this time: "Devam Et" replaces the cart with the parked one.
    await page.locator('[data-action="recall"]').click();
    await expect(confirmModal).toBeVisible();
    await page.getByRole('button', { name: 'Devam Et' }).click();

    await expect(confirmModal).toBeHidden();
    await expect(page.locator('#parkedModal')).toBeHidden();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');
    await expect(page.locator('#parkCountBadge')).toHaveText('0');
  });
});
