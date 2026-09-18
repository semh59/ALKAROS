import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';

// V13-PUI-002: the "ikinci açık oturum engellenir" acceptance criterion -
// two tabs load the "no active session" screen before either has opened
// one (same terminalId - same browser context/localStorage, same cookie),
// then both try to open. The second POST must be rejected (409
// ACTIVE_CASH_SESSION_EXISTS) and the page must show the real conflict
// screen (Kasa-Cakisma.dc.html's production counterpart), not a generic
// error or a silently-created second session.
test.describe('Kasa Oturumu - çakışma (V13-PUI-002)', () => {
  test('aynı terminalde eşzamanlı ikinci açılış çakışma ekranını gösterir', async ({ context }) => {
    const seed = readSeed();

    const firstPage = await context.newPage();
    await firstPage.goto('/cashier/payments/cash-session/index.html');
    await firstPage.locator('#username').fill(seed.cashierUsername);
    await firstPage.locator('#password').fill(seed.cashierPassword);
    await firstPage.getByRole('button', { name: 'Giriş Yap' }).click();
    await expect(firstPage.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });

    // Aynı context => aynı çerez zaten kurulu, ikinci sekme kendi giriş
    // formunu hiç görmeden doğrudan "açık oturum yok" ekranına gelir.
    const secondPage = await context.newPage();
    await secondPage.goto('/cashier/payments/cash-session/index.html');
    await expect(secondPage.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });

    await firstPage.locator('#opening-balance').fill('300');
    await firstPage.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(firstPage.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await secondPage.locator('#opening-balance').fill('300');
    await secondPage.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(secondPage.getByRole('heading', { name: 'Bu Terminalde Zaten Açık Bir Kasa Var' })).toBeVisible();

    await secondPage.getByRole('button', { name: 'Mevcut Oturuma Git' }).click();
    await expect(secondPage.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await secondPage.close();
    await firstPage.close();
  });
});
