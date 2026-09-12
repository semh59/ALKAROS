import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

test.describe('Giriş ekranı', () => {
  test('yanlış şifre reddedilir, geçerli bilgiler masalar ekranını açar', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('#loginOverlay')).toBeVisible();

    await page.locator('#loginUsername').fill(seed.waiterUsername);
    await page.locator('#loginPassword').fill('yanlis-sifre');
    await page.locator('#loginSubmit').click();

    await expect(page.locator('#loginError')).toBeVisible();
    await expect(page.locator('#loginError')).toContainText('hatalı');
    // A rejected login must not leak into the app - still on the overlay.
    await expect(page.locator('#loginOverlay')).toBeVisible();

    await page.locator('#loginPassword').fill(seed.waiterPassword);
    await page.locator('#loginSubmit').click();

    await expect(page.locator('#loginOverlay')).toBeHidden();
    await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
    await expect(page.locator(`[data-table="${seed.tableId}"]`)).toContainText(seed.tableNumber);
  });
});
