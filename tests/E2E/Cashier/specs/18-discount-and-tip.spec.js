import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment, selectMethod, setAmount } from '../lib/paymentHelpers.js';

// V1-RMD-292: billing/bills/{id}/discount, /tip and (read-only) /adjustments had no client.
//
// A real, deep server-side finding surfaced while wiring this (verified by reading the code, not fixed here -
// Host/Modules, outside this task's Owned surface of split-payment.js alone; V1-RMD-298 opened for it):
// bill.PayableAmount never updates when a discount/tip is recorded, and the two places that actually gate
// money (PaymentAllocationFactory's tender ceiling, BillPaymentClosureCalculator's "is this bill fully paid"
// check) both use that same raw, never-adjusted amount. So a discount/tip is real and persisted, but does NOT
// change what the till must still collect to close the bill, and a tip can never itself be tendered past the
// bill's original amount. This screen tells the cashier that honestly instead of showing a wrong "Kalan".
test.describe('Hesap Ödeme - indirim ve gönüllü bahşiş (V1-RMD-292)', () => {
  test('indirim ve bahşiş kaydedilir ve görünür; tahsilat tavanı (dürüstçe) hâlâ hesabın ham tutarıdır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100 ₺

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺100,00');

    // %10 indirim: gerçek bir sunucu kaydı oluşur ve görünür.
    await page.locator('#discount-calc-type').selectOption('Percentage');
    await page.locator('#discount-value').fill('10');
    await page.getByRole('button', { name: 'İndirim uygula' }).click();
    await expect(page.getByText('İndirim uygulandı ve kaydedildi.')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.sp-line-method', { hasText: 'İndirim (%)' })).toBeVisible();
    await expect(page.locator('.sp-line-method', { hasText: 'Kaydedilen düzeltmelerle toplam' })).toBeVisible();

    // 20 ₺ gönüllü bahşiş: aynı şekilde gerçek bir kayıt.
    await page.locator('#tip-amount').fill('20');
    await page.getByRole('button', { name: 'Bahşiş ekle' }).click();
    await expect(page.locator('.sp-line-method', { hasText: 'Bahşiş' })).toBeVisible({ timeout: 10_000 });

    // Dürüst tavan: "Kalan" hâlâ hesabın ham tutarı (100 ₺) - indirim/bahşiş bunu değiştirmedi (bulgu).
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toHaveText('₺100,00');
    await expect(page.getByText('Bu tutar bilgi amaçlıdır')).toBeVisible();

    // Sunucu gerçeği: kayıtlı düzeltmeler gerçekten `AdjustmentCalculator.Calculate`'ten geliyor (90 - indirim
    // + 20 bahşiş = 110), ayrı bir GET ile doğrulanıyor - ekranın kendi metnini tekrar okumak değil.
    const adjustments = await (await page.request.get(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/adjustments`)).json();
    expect(adjustments.summary.adjustedPayableAmount).toBe(110);
    expect(adjustments.adjustments).toHaveLength(2);

    // Ham tutarın tamamı (100 ₺) hâlâ gerçekten tahsil edilebiliyor.
    await selectMethod(page, 'Eft');
    await setAmount(page, '100');
    await page.locator('#eft-confirm').check();
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
  });

  test('bahşiş dahil tutar tahsil edilmeye çalışılırsa sunucu reddeder (tavan ham tutardır - bulgu)', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100 ₺
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await page.locator('#tip-amount').fill('20');
    await page.getByRole('button', { name: 'Bahşiş ekle' }).click();
    await expect(page.locator('.sp-line-method', { hasText: 'Bahşiş' })).toBeVisible({ timeout: 10_000 });

    // Ekranın kendi sınırı hâlâ 100 ₺'yi aşan bir tutara izin vermiyor (client-side guard, remainingAmount()).
    await selectMethod(page, 'Eft');
    await setAmount(page, '120');
    await page.locator('#eft-confirm').check();
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(page.getByText('Tutar kalan tutarı aşamaz.')).toBeVisible({ timeout: 5_000 });
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
