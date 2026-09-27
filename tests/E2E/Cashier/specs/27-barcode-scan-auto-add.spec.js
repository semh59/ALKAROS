import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-376 (module-by-module UI audit round 2, P2/P1 - saha gerçekliği/rakip
// karşılaştırması): a barcode scanner "types" a SKU into whichever field has
// focus, then stops - nothing here ever acted on the finished scan, so it
// just sat in the search box requiring the cashier to look down and tap the
// matching card by hand on every single item, unlike a real POS register
// (Toast/Square), which auto-adds the moment a scan resolves to exactly one
// SKU and clears the field for the next scan.
test.describe('Kasiyer - barkod taraması otomatik ekleme (V1-RMD-376)', () => {
  test('tam SKU eşleşmesi ürünü otomatik ekler ve arama kutusunu temizler', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const searchInput = page.locator('#searchInput');
    await expect(searchInput).toBeVisible({ timeout: 15_000 });
    await searchInput.fill('E2E-KASA-DUSUK');

    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Düşük Stok');
    await expect(searchInput).toHaveValue('');
  });

  test('modifikatörlü bir ürünün SKU taraması doğrudan eklemez, seçenek modalını açar', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const searchInput = page.locator('#searchInput');
    await expect(searchInput).toBeVisible({ timeout: 15_000 });
    await searchInput.fill('E2E-KASA-BOYLU');

    await expect(page.locator('#modifierModal')).toBeVisible();
    await expect(page.locator('.ticket-row')).toHaveCount(0);
  });

  test('kısmi eşleşme (gerçek arama) hâlâ yalnızca filtreler, otomatik eklemez', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const searchInput = page.locator('#searchInput');
    await expect(searchInput).toBeVisible({ timeout: 15_000 });
    await searchInput.fill('E2E-KASA');

    await expect(page.locator('.ticket-row')).toHaveCount(0);
    await expect(page.locator('.pos-product-card')).toHaveCount(3);
  });

  // V1-RMD-376: `query` was already lowercased for this comparison, but the
  // catalog's own `code` field never was - SKUs are conventionally uppercase
  // ("E2E-KASA-DUSUK"), so a case-sensitive .includes() silently matched
  // nothing for any lowercase code search at all (a cashier typing a SKU by
  // hand, or a scanner sending a different case than the catalog record).
  test('küçük harfle yazılan SKU araması da eşleşir (kod karşılaştırması büyük/küçük harfe duyarsız)', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const searchInput = page.locator('#searchInput');
    await expect(searchInput).toBeVisible({ timeout: 15_000 });
    await searchInput.fill('e2e-kasa-normal');

    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Normal Stok');
  });
});
