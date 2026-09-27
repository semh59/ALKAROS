import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-360 (module-by-module UI audit, 2026-09-27): a batch of findings from the vanilla
// Cashier main-screen module, each proven and fixed independently.
test.describe('Cashier ana ekran - aktif sepet kalıcılığı ve modal odak yönetimi (V1-RMD-360)', () => {
  test('aktif sepet sayfa yenilemesinde kaybolmaz', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });
    await lowStockCard.click();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');

    // A real reload - the exact scenario (crash, accidental refresh, browser restart) the fix
    // targets, not a soft client-side re-render.
    await page.reload();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok', { timeout: 15_000 });

    // Clearing it (with the new confirmation) removes the persisted copy too, not just the DOM.
    await page.locator('#btnClearTicket').click();
    await page.getByRole('button', { name: 'Devam Et' }).click();
    await expect(page.locator('.ticket-empty')).toBeVisible();
    await page.reload();
    await expect(page.locator('.ticket-empty')).toBeVisible({ timeout: 15_000 });
  });

  test('"Fişi Temizle" onay ister; "Vazgeç" sepeti korur, "Devam Et" gerçekten temizler', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });
    await lowStockCard.click();

    await page.locator('#btnClearTicket').click();
    const confirmModal = page.locator('#confirmModal');
    await expect(confirmModal).toBeVisible();
    await page.getByRole('button', { name: 'Vazgeç' }).click();
    await expect(confirmModal).toBeHidden();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');

    await page.locator('#btnClearTicket').click();
    await page.getByRole('button', { name: 'Devam Et' }).click();
    await expect(page.locator('.ticket-empty')).toBeVisible();
  });

  test('modaller açılınca odağı içine alır, Escape ile kapanır ve odağı geri verir', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    // #confirmModal: opened from "Fişi Temizle".
    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });
    await lowStockCard.click();
    await page.locator('#btnClearTicket').focus();
    await page.locator('#btnClearTicket').click();

    const confirmModal = page.locator('#confirmModal');
    await expect(confirmModal).toBeVisible();
    // Focus must have moved INTO the dialog - "Vazgeç" is its first focusable control.
    await expect(page.getByRole('button', { name: 'Vazgeç' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(confirmModal).toBeHidden();
    // Escape on a confirm dialog is the same answer as "Vazgeç" - the cart survives.
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');
    // Focus returns to whatever opened the dialog.
    await expect(page.locator('#btnClearTicket')).toBeFocused();

    // #parkedModal: opened from "Fişler" (Geri Yükle).
    await page.locator('#btnParkTicket').click();
    await page.locator('#btnRecallTicket').focus();
    await page.locator('#btnRecallTicket').click();
    const parkedModal = page.locator('#parkedModal');
    await expect(parkedModal).toBeVisible();
    await expect(page.locator('#btnCloseParkedModal')).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(parkedModal).toBeHidden();
    await expect(page.locator('#btnRecallTicket')).toBeFocused();
  });

  test('kategori sekmeleri tab/aria-selected taşır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const allTab = page.locator('.category-tabs .tab-chip', { hasText: 'Tüm Ürünler' });
    await expect(allTab).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.category-tabs')).toHaveAttribute('role', 'tablist');
    await expect(allTab).toHaveAttribute('role', 'tab');
    await expect(allTab).toHaveAttribute('aria-selected', 'true');
  });

  test('arama kutusu sayfa yüklenince ve sipariş mutfağa gönderilince odaklanır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const searchInput = page.locator('#searchInput');
    await expect(searchInput).toBeVisible({ timeout: 15_000 });
    await expect(searchInput).toBeFocused();

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await lowStockCard.click();
    await page.locator('#btnDispatchOrder').click();
    await expect(page.locator('#toastRegion .toast--success')).toBeVisible({ timeout: 10_000 });
    await expect(searchInput).toBeFocused();
  });
});
