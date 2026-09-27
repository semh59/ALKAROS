import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-376 (module-by-module UI audit round 2, P1 - rakip karşılaştırması):
// a product with a MANDATORY option group used to add straight to the
// ticket with zero chance to ever capture it, silently and with no error
// from either side (the server's own CatalogModifierGroupDto doc comment:
// "the server does not enforce them yet" - enforcement is deliberately a
// client responsibility). Every real competitor register offers this at
// the point of sale; WaiterPwa's own catalog already carries the data.
test.describe('Kasiyer - zorunlu seçenek grubu (V1-RMD-376)', () => {
  test('zorunlu grup seçilmeden Adisyona Ekle kapalı kalır; seçince adisyona doğru satır düşer', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const modifierCard = page.locator(`.pos-product-card[data-product-id="${seed.modifierProductId}"]`);
    await expect(modifierCard).toBeVisible({ timeout: 15_000 });
    await modifierCard.click();

    const modal = page.locator('#modifierModal');
    await expect(modal).toBeVisible();
    const confirmButton = page.locator('#btnModifierConfirm');
    await expect(confirmButton).toBeDisabled();

    // No ticket line yet - the product must not be added before the
    // mandatory group is satisfied.
    await expect(page.locator('.ticket-row')).toHaveCount(0);

    await page.getByText('Büyük', { exact: true }).click();
    await expect(confirmButton).toBeEnabled();
    await confirmButton.click();

    await expect(modal).toBeHidden();
    const row = page.locator('.ticket-row');
    await expect(row.locator('.item-title')).toHaveText('E2E Boylu Ürün');
    await expect(row.locator('.item-modifiers')).toHaveText('Büyük');
    // Base 150,00 + Büyük's own +25,00 price delta.
    await expect(row.locator('.item-sub')).toContainText('175,00');
  });

  test('Vazgeç ve Escape adisyona hiçbir şey eklemeden modal\'ı kapatır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const modifierCard = page.locator(`.pos-product-card[data-product-id="${seed.modifierProductId}"]`);
    await expect(modifierCard).toBeVisible({ timeout: 15_000 });
    await modifierCard.click();
    await expect(page.locator('#modifierModal')).toBeVisible();

    await page.getByRole('button', { name: 'Vazgeç' }).click();
    await expect(page.locator('#modifierModal')).toBeHidden();
    await expect(page.locator('.ticket-row')).toHaveCount(0);

    await modifierCard.click();
    await expect(page.locator('#modifierModal')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.locator('#modifierModal')).toBeHidden();
    await expect(page.locator('.ticket-row')).toHaveCount(0);
  });

  test('farklı seçimlerle eklenen iki satır birleşmez; katalogdaki bir ürün modifikatörsüz de eklenmeye devam eder', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const modifierCard = page.locator(`.pos-product-card[data-product-id="${seed.modifierProductId}"]`);
    await expect(modifierCard).toBeVisible({ timeout: 15_000 });

    await modifierCard.click();
    await page.getByText('Küçük', { exact: true }).click();
    await page.locator('#btnModifierConfirm').click();

    await modifierCard.click();
    await page.getByText('Büyük', { exact: true }).click();
    await page.locator('#btnModifierConfirm').click();

    await expect(page.locator('.ticket-row')).toHaveCount(2);
    await expect(page.locator('.ticket-row').nth(0).locator('.item-modifiers')).toHaveText('Küçük');
    await expect(page.locator('.ticket-row').nth(1).locator('.item-modifiers')).toHaveText('Büyük');

    // A plain, no-modifier product is completely unaffected.
    const lowStockCard = page.locator(`.pos-product-card[data-product-id="${seed.lowStockProductId}"]`);
    await lowStockCard.click();
    await expect(page.locator('.ticket-row')).toHaveCount(3);
    await expect(page.locator('#modifierModal')).toBeHidden();
  });
});
