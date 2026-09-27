import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';

// V1-RMD-377 (module-by-module UI audit round 2, P1 - rakip karşılaştırması):
// a real competitor register (Toast/Square/Clover) counts a drawer by kupür
// (banknote/coin), not one free-typed number the cashier has to mentally add
// up - one keystroke error in that mental math used to become an unexplained
// variance at close with no way to trace which denomination it came from.
test.describe('Kasa Oturumu - kupür bazlı sayım (V1-RMD-377)', () => {
  async function openToCountingScreen(page, seed, openingBalance) {
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.locator('#username').fill(seed.cashierUsername);
    await page.locator('#password').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill(String(openingBalance));
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
  }

  test('kupür sayıları girildikçe toplam otomatik hesaplanır ve o tutar kaydedilir', async ({ page }) => {
    const seed = readSeed();
    await openToCountingScreen(page, seed, 0);

    // Varsayılan mod kupür bazlı - eski tek-tutar alanı hiç görünmez.
    await expect(page.locator('#counted-amount')).toHaveCount(0);
    await expect(page.locator('#denom-total-value')).toHaveText('₺0,00');

    await page.locator('#denom-200').fill('2'); // 400
    await page.locator('#denom-50').fill('1'); // 50
    await page.locator('#denom-1').fill('3'); // 3
    // 2×200 + 1×50 + 3×1 = 453
    await expect(page.locator('#denom-total-value')).toHaveText('₺453,00');

    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();
    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.getByText('₺453,00').first()).toBeVisible();
  });

  test('"Kupürüm yok, tek tutar gireceğim" eski manuel alana geçer; geri dönülebilir', async ({ page }) => {
    const seed = readSeed();
    await openToCountingScreen(page, seed, 0);

    await page.getByRole('button', { name: 'Kupürüm yok, tek tutar gireceğim' }).click();
    await expect(page.locator('#counted-amount')).toBeVisible();
    await expect(page.locator('.cs-denom-row')).toHaveCount(0);

    await page.getByRole('button', { name: 'Kupür bazlı saymaya dön' }).click();
    await expect(page.locator('#counted-amount')).toHaveCount(0);
    await expect(page.locator('.cs-denom-row').first()).toBeVisible();
  });

  test('boş bırakılan kupürler sıfır sayılır, negatif adet girilemez', async ({ page }) => {
    const seed = readSeed();
    await openToCountingScreen(page, seed, 0);

    await page.locator('#denom-100').fill('1');
    await expect(page.locator('#denom-total-value')).toHaveText('₺100,00');

    // A negative count would make no physical sense - min="0" on the input
    // and the handler's own Math.max(0, ...) both guard it.
    await expect(page.locator('#denom-100')).toHaveAttribute('min', '0');
  });
});
