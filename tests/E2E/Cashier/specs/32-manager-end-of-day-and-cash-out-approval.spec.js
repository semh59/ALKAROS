import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { createBill, loginViaApi } from '../lib/paymentHelpers.js';

// Two managers on the real Host: a cash-out above the approval threshold is requested by the cashier and can only
// be approved by the other manager; the manager then closes the business day from the Yönetim area.
// A terminal belongs to one device key, so the second manager takes over the cashier's own terminal after the
// cashier signs out; the open cash session stays on the drawer.
async function switchToSecondManager(page, seed, terminalId) {
  const logout = await page.request.post(`/api/v1/auth/logout?terminalId=${terminalId}`);
  expect(logout.ok(), `logout failed: ${logout.status()}`).toBeTruthy();
  const login = await page.request.post('/api/v1/auth/login', {
    data: { Username: seed.secondManagerUsername, Password: seed.cashierPassword, TerminalId: terminalId },
  });
  expect(login.ok(), `second manager login failed: ${login.status()}`).toBeTruthy();
}

test.describe('Yönetici onayı ve gün sonu', () => {
  test('eşik üstü nakit çıkışı kasiyer onaylayamaz; ikinci yönetici onaylayınca tutar kasadan düşer', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    await page.goto('/cashier/payments/cash-session/index.html');
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('2000');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Nakit Çıkış' }).click();
    await page.locator('#movement-amount').fill('800');
    await page.locator('#movement-notes').fill('E2E test - ikinci yönetici onayı');
    await page.getByRole('button', { name: 'Devam' }).click();
    await page.getByRole('button', { name: 'Onaya gönder' }).click();
    await expect(page.locator('#pending-requests')).toContainText('E2E test - ikinci yönetici onayı');
    await expect(page.locator('[data-request-action="approve"]')).toHaveCount(0);
    await expect(page.locator('#pending-requests')).toContainText('Kendi talebinizi onaylayamazsınız');

    await switchToSecondManager(page, seed, terminalId);
    await page.goto('/cashier/payments/cash-session/index.html');
    await expect(page.locator('#pending-requests')).toContainText('E2E test - ikinci yönetici onayı', { timeout: 15_000 });
    await page.locator('[data-request-action="approve"]').click();
    await expect(page.locator('#request-confirm-text')).toContainText('kasadan düşülecek');
    await page.getByRole('button', { name: 'Evet, onayla' }).click();
    await expect(page.locator('#cs-notice')).toHaveText('Çıkış onaylandı ve kasadan düşüldü.');
    await expect(page.locator('#pending-requests')).toHaveCount(0);
    await expect(page.locator('#movement-list')).toContainText('E2E test - ikinci yönetici onayı');
  });

  test('yönetici iş gününü açar, açık hesap uyarısını onaylayarak kapatır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    await createBill(page, terminalId, seed);
    await page.context().clearCookies();

    await page.goto('/');
    await page.getByLabel('Kullanıcı adı').fill(seed.secondManagerUsername);
    await page.getByLabel('Parola').fill(seed.cashierPassword);
    await page.getByRole('button', { name: /Giriş yap/ }).click();
    await page.getByRole('link', { name: 'Yönetim' }).first().click();
    await page.getByRole('button', { name: 'Gün sonu ve mutabakat' }).click();

    const openDay = page.getByRole('button', { name: 'İş gününü aç' });
    const closeDay = page.getByRole('button', { name: 'İş gününü kapat' });
    await expect(openDay.or(closeDay)).toBeVisible({ timeout: 15_000 });
    if (await openDay.isVisible()) {
      await openDay.click();
      await expect(page.getByText('İş günü açıldı.')).toBeVisible();
    }

    await expect(closeDay).toBeVisible();
    const summary = page.getByLabel('Kapanış özeti');
    await expect(summary).toBeVisible();
    await expect(summary.locator('div', { hasText: 'Açık hesap' }).first().locator('dd')).not.toHaveText('0');
    const acknowledge = page.getByLabel('Bunları bilerek kapatıyorum');
    await expect(acknowledge).toBeVisible();
    await expect(closeDay).toBeDisabled();
    await acknowledge.check();
    await expect(closeDay).toBeEnabled();
    await closeDay.click();
    await expect(page.getByText('İş günü kapatıldı.')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('button', { name: 'İş gününü kapat' })).toHaveCount(0);
  });
});
