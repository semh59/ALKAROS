import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';

// V1-RMD-361 (module-by-module UI audit, 2026-09-27): the Kasa Oturumu (cash-session) module.
test.describe('Kasa Oturumu - erişilebilirlik ve odak yönetimi (V1-RMD-361)', () => {
  test('hata kutusu role="alert" taşır; sekmeler role="tab"/aria-selected taşır', async ({ page }) => {
    const seed = readSeed();
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.locator('#username').fill(seed.cashierUsername);
    await page.locator('#password').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    // A genuinely invalid amount (negative) triggers the client-side validation error.
    await page.locator('#opening-balance').fill('-5');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.locator('.cs-alert-danger')).toHaveAttribute('role', 'alert');

    await page.locator('#opening-balance').fill('0');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Nakit Giriş' }).click();
    await expect(page.locator('.cs-tabs')).toHaveAttribute('role', 'tablist');
    const tabIn = page.locator('#tab-in');
    const tabOut = page.locator('#tab-out');
    await expect(tabIn).toHaveAttribute('role', 'tab');
    await expect(tabIn).toHaveAttribute('aria-selected', 'true');
    await expect(tabOut).toHaveAttribute('aria-selected', 'false');
    await tabOut.click();
    await expect(tabOut).toHaveAttribute('aria-selected', 'true');
    await expect(tabIn).toHaveAttribute('aria-selected', 'false');
  });

  test('her yeni ekranda birincil alana otomatik odaklanır', async ({ page }) => {
    const seed = readSeed();
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.locator('#username').fill(seed.cashierUsername);
    await page.locator('#password').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('#opening-balance')).toBeFocused();

    await page.locator('#opening-balance').fill('0');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
    // V1-RMD-377 (module-by-module UI audit round 2): the counting screen
    // now defaults to a kupür (banknote/coin) breakdown - the first field is
    // its highest-denomination count, not the old single free-typed amount.
    await expect(page.locator('#denom-200')).toBeFocused();
  });
});
