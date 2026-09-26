import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment, selectMethod, setAmount } from '../lib/paymentHelpers.js';

// V1-RMD-292: billing/bills/{id}/discount, /tip and (read-only) /adjustments had no client.
//
// V1-RMD-298 (independent 2026-09-26 audit, finding K1, fixed): bill.PayableAmount never updates when a
// discount/tip is recorded, and the places that actually gate money (PaymentAllocationFactory's tender
// ceiling, BillPaymentClosureCalculator's "is this bill fully paid" check, CashTenderHandler/EftTenderHandler's
// own pre-checks, the tender-summary GET this screen reads) all used to read that same raw, never-adjusted
// amount. Fixed to read AdjustmentCalculator's AdjustedPayableAmount instead - a discount/tip now genuinely
// changes what the till must collect and when the bill closes, matching what this screen has always shown.
test.describe('Hesap Ödeme - indirim ve gönüllü bahşiş (V1-RMD-292 / V1-RMD-298)', () => {
  test('indirim ve bahşiş kaydedilir; tahsilat tavanı düzeltilmiş tutara göre gerçekten değişir', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100 ₺

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺100,00');

    // %10 indirim: gerçek bir sunucu kaydı oluşur ve görünür, tavan hemen 90 ₺'ye düşer.
    await page.locator('#discount-calc-type').selectOption('Percentage');
    await page.locator('#discount-value').fill('10');
    await page.getByRole('button', { name: 'İndirim uygula' }).click();
    await expect(page.getByText('İndirim uygulandı ve kaydedildi.')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.sp-line-method', { hasText: 'İndirim (%)' })).toBeVisible();
    await expect(page.locator('.sp-line-method', { hasText: 'Kaydedilen düzeltmelerle toplam' })).toBeVisible();
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺90,00');

    // 20 ₺ gönüllü bahşiş: aynı şekilde gerçek bir kayıt, tavan 110 ₺'ye çıkar.
    await page.locator('#tip-amount').fill('20');
    await page.getByRole('button', { name: 'Bahşiş ekle' }).click();
    await expect(page.locator('.sp-line-method', { hasText: 'Bahşiş' })).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺110,00');

    // Sunucu gerçeği: kayıtlı düzeltmeler gerçekten `AdjustmentCalculator.Calculate`'ten geliyor (90 - indirim
    // + 20 bahşiş = 110), ayrı bir GET ile doğrulanıyor - ekranın kendi metnini tekrar okumak değil.
    const adjustments = await (await page.request.get(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/adjustments`)).json();
    expect(adjustments.summary.adjustedPayableAmount).toBe(110);
    expect(adjustments.adjustments).toHaveLength(2);

    // Düzeltilmiş tutarın tamamı (110 ₺) gerçekten tahsil edilip hesap kapatılabiliyor.
    await selectMethod(page, 'Eft');
    await setAmount(page, '110');
    await page.locator('#eft-confirm').check();
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
  });

  test('bahşiş dahil düzeltilmiş tutar artık gerçekten tahsil edilebiliyor; onu aşan tutar hâlâ reddedilir', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100 ₺
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await page.locator('#tip-amount').fill('20');
    await page.getByRole('button', { name: 'Bahşiş ekle' }).click();
    await expect(page.locator('.sp-line-method', { hasText: 'Bahşiş' })).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺120,00');

    // K1 fix: 120 ₺ (ham 100 ₺'yi aşan, gerçek düzeltilmiş tavan) artık gerçekten kabul ediliyor - eskiden
    // sunucu bunu OverAllocationException/EftOverTenderException ile reddederdi.
    await selectMethod(page, 'Eft');
    await setAmount(page, '120');
    await page.locator('#eft-confirm').check();
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
  });

  test('yetkisiz kullanıcı reddedilir: indirim isteği yönetici onayına düşer, ret Türkçe gösterilir', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await page.route('**/billing/bills/**/discount', (route) => route.fulfill({
      status: 403,
      contentType: 'application/json',
      body: JSON.stringify({ error: { code: 'GRANT_DENIED', message: 'Discount request was denied.' } }),
    }));
    await page.locator('#discount-value').fill('10');
    await page.getByRole('button', { name: 'İndirim uygula' }).click();

    // Sunucunun kendi mesajı İngilizce sızdırıyor (ayrı, bu görevin Owned surface'ı dışındaki bir bulgu);
    // bu ekran GRANT_DENIED kodunu kendi Türkçe metniyle geçersiz kılıyor.
    await expect(page.getByText('İndirim talebiniz reddedildi. Yetkili bir kullanıcı uygulayabilir.')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Discount request was denied.')).toHaveCount(0);
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺100,00');
  });
});
