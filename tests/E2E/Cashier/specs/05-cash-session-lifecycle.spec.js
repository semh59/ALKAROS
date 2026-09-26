import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';

// V13-PUI-002: the standalone Kasa Oturumu page (src/Clients/Cashier/
// wwwroot/payments/cash-session/**) exercised end to end against the real
// Host + real Postgres this suite already boots - no mocked fetch, the same
// backend V13-CSH-004 wired up.
async function loginOnCashSessionPage(page, seed) {
  // 02-stock-badge-and-kitchen-dispatch.spec.js's own precedent: UseDefaultFiles
  // does not resolve a bare directory URL to index.html here, only an explicit
  // filename does - same gap, same workaround.
  await page.goto('/cashier/payments/cash-session/index.html');
  await page.locator('#username').fill(seed.cashierUsername);
  await page.locator('#password').fill(seed.cashierPassword);
  await page.getByRole('button', { name: 'Giriş Yap' }).click();
}

test.describe('Kasa Oturumu (V13-PUI-002)', () => {
  test('açılış, nakit giriş, sayım ve sıfır farkla kapatma tam akışı', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('#opening-balance')).toHaveValue('0.00');
    await page.locator('#opening-balance').fill('500');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();

    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await expect(page.locator('.cs-status-pill')).toHaveText(/Açık/);

    // Manuel nakit giriş - banka için çekilen 50 TL'nin ardından geri
    // yatırılan bir tutar senaryosu.
    await page.getByRole('button', { name: 'Nakit Giriş' }).click();
    await page.locator('#movement-amount').fill('50');
    await page.locator('#movement-notes').fill('E2E test - manuel giriş');
    await page.getByRole('button', { name: 'Girişi Kaydet' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    // Sayım: 500 açılış + 50 nakit giriş = 550 beklenen; tam o kadar sayılır.
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
    await page.locator('#counted-amount').fill('550');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.getByText('₺550,00').first()).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();

    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('₺0,00').last()).toBeVisible();
  });

  // V1-RMD-343 (independent 2026-09-26 audit, orta seviye bulgu): a parked
  // (beklet) cart had no vardiya (shift) boundary at all - it stayed in
  // localStorage indefinitely, recallable by whoever opened the NEXT shift
  // on this same terminal. Closing the shift is exactly that boundary.
  test('kasa kapatıldığında bekletilen sepetler temizlenir', async ({ page }) => {
    const seed = readSeed();
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.evaluate(() => {
      localStorage.setItem('alkaros_cashier_parked', JSON.stringify([
        { id: 'e2e-parked-1', items: [{ productName: 'Test', quantity: 1, unitPrice: 10 }], parkedAt: '12:00' },
      ]));
    });
    expect(await page.evaluate(() => localStorage.getItem('alkaros_cashier_parked'))).not.toBeNull();

    await page.locator('#username').fill(seed.cashierUsername);
    await page.locator('#password').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('0');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();

    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
    await page.locator('#counted-amount').fill('0');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();

    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
    expect(await page.evaluate(() => localStorage.getItem('alkaros_cashier_parked'))).toBeNull();
  });

  test('kapalı süpervizör toleransını aşan fark için açıklama zorunlu tutulur', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('100');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    // Beklenenden çok farklı bir sayım - süpervizör onay akışını tetikler.
    await page.locator('#counted-amount').fill('40');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    // İlk deneme: sunucu farkın tolerans sınırını (varsayılan 50 TL) aştığını
    // 409 CASH_VARIANCE_THRESHOLD_EXCEEDED ile bildirir - bu, ekranın kendi
    // sabit bir eşik değerini kopyalamadığının kanıtı (backend akıllı,
    // frontend aptal).
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByText('Fark tolerans sınırını aşıyor').first()).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByText('bir açıklama girin')).toBeVisible();

    await page.locator('#override-reason').fill('E2E test - sayım hatası, tutar teyit edildi');
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
  });
});
