import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment } from '../lib/paymentHelpers.js';

// V1-RMD-356 (independent 2026-09-26 audit, a low-severity finding): three accessibility gaps across the
// vanilla Cashier client's static pages - a product card that was focusable (tabindex="0" role="button") but
// never listened for Enter/Space, symbol-only quantity buttons ("−"/"+") with no aria-label unlike their
// sibling delete button, and several form fields whose only "label" was a visible <span>/placeholder never
// programmatically tied to the input (getByLabel/a screen reader cannot resolve either). Fixed without moving
// any markup: a keydown handler mirroring the existing click handler, aria-label additions matching the
// existing "Sil" button's own pattern, and <label for="..."> wiring (cash-session.js) or aria-label
// (split-payment.js, whose <span class="sp-field-label"> describes a whole field GROUP, not one input).
test.describe('Kasa erişilebilirlik: klavye ve etiketler (V1-RMD-356)', () => {
  test('ürün kartı Enter/Space ile sepete eklenir; miktar düğmeleri ve arama kutusu adlandırılmıştır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await expect(lowStockCard).toBeVisible({ timeout: 15_000 });

    // The card is reachable and activatable purely from the keyboard, with no mouse click at all.
    await lowStockCard.focus();
    await page.keyboard.press('Enter');
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');
    await expect(page.locator('.ticket-row-qty')).toHaveText('1');

    await lowStockCard.focus();
    await page.keyboard.press(' ');
    await expect(page.locator('.ticket-row-qty')).toHaveText('2');

    // The quantity buttons are individually addressable by a screen reader (not just "−"/"+" glyphs) and
    // named after the specific ticket line they affect, same as the pre-existing "Sil" button.
    await expect(page.getByRole('button', { name: 'E2E Kasa Düşük Stok adedini azalt' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'E2E Kasa Düşük Stok adedini artır' })).toBeVisible();
    await page.getByRole('button', { name: 'E2E Kasa Düşük Stok adedini azalt' }).click();
    await expect(page.locator('.ticket-row-qty')).toHaveText('1');

    // The search input has a persistent accessible name, not just a placeholder that vanishes once typed into.
    await expect(page.getByLabel('Barkod okutun veya ürün adı arayın')).toBeVisible();
  });

  test('Kasa Oturumu giriş ve açılış alanları <label for> ile programatik olarak bağlanmıştır', async ({ page }) => {
    const seed = readSeed();
    await page.goto('/cashier/payments/cash-session/index.html');

    // getByLabel only resolves through a real <label for="..."> (or aria-label) - a bare, unassociated
    // <span class="cs-field-label"> would fail this exact assertion.
    await page.getByLabel('Kullanıcı adı').fill(seed.cashierUsername);
    await page.getByLabel('Şifre').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel('Açılış Tutarı')).toHaveValue('0.00');
  });

  test('Tahsilat ekranındaki indirim ve bahşiş alanları erişilebilir adlar taşır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    // Each of these has only a group-level <span class="sp-field-label"> in the markup (describing the whole
    // form, e.g. "İndirim / düzeltme ekle"), never one bound to the individual input - aria-label is what
    // makes getByLabel (and a screen reader) resolve them at all.
    await expect(page.getByLabel('İndirim gerekçesi')).toBeVisible();
    await expect(page.getByLabel('İndirim hesaplama türü')).toBeVisible();
    await expect(page.getByLabel('İndirim değeri')).toBeVisible();
    await expect(page.getByLabel('İndirim notu (opsiyonel)')).toBeVisible();
    await expect(page.getByLabel('Bahşiş tutarı')).toBeVisible();
    await expect(page.getByLabel('Bahşiş notu (opsiyonel)')).toBeVisible();
  });
});
