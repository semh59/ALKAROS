import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment } from '../lib/paymentHelpers.js';

// V1-RMD-362 (module-by-module UI audit, 2026-09-27): the Hesap Ödeme (split-payment) module.
test.describe('Hesap Ödeme - erişilebilirlik (V1-RMD-362)', () => {
  test('hata/uyarı kutuları role taşır; ödeme yöntemi seçimi radiogroup/radio semantiğine sahip', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await expect(page.locator('.sp-method-chips')).toHaveAttribute('role', 'radiogroup');
    // This test's cashier has no open cash session (loginViaApi/createBill open no kasa oturumu),
    // so the page's own default lands on Eft (state.cashSessionOpen is false) - real, current
    // behavior, not an assumption this test invents.
    const eftChip = page.locator('.sp-method-chip', { hasText: 'EFT/Havale' });
    await expect(eftChip).toHaveAttribute('role', 'radio');
    await expect(eftChip).toHaveAttribute('aria-checked', 'true');
    const cardChip = page.locator('.sp-method-chip', { hasText: 'Kredi/Banka Kartı' });
    await expect(cardChip).toHaveAttribute('aria-checked', 'false');
    await cardChip.click();
    await expect(cardChip).toHaveAttribute('aria-checked', 'true');
    await expect(eftChip).toHaveAttribute('aria-checked', 'false');

    // A real client-side validation error - a zero amount.
    await page.locator('#amount-draft').fill('0');
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(page.locator('.sp-alert-danger')).toHaveAttribute('role', 'alert');

    // A real discount success notice.
    await page.locator('#discount-value').fill('10');
    await page.getByRole('button', { name: 'İndirim uygula' }).click();
    await expect(page.getByText('İndirim uygulandı ve kaydedildi.')).toHaveAttribute('role', 'status');
  });
});
