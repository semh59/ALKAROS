import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

// V1-RMD-379 (module-by-module UI audit round 2, T8 - mobil/performans):
// renderProducts() rebuilt the whole visible list on every single keystroke
// with no debounce - a real stutter risk on a modest phone CPU with a large
// catalog, the same class of gap Cashier's own vanilla search already had
// fixed for it (V1-RMD-360).
test.describe('Menü arama - debounce (V1-RMD-379)', () => {
  test('yazarken hemen filtrelemez, 120ms sonra doğru sonuca yerleşir', async ({ page }) => {
    const seed = readSeed();
    await login(page, seed);

    // Whether the seeded table is still empty or already occupied depends
    // on which other specs ran before this one in a full-suite run (an
    // occupied table opens straight onto the bill sheet; a still-empty one
    // opens the menu screen first) - 10-toast-roles.spec.js's own note on
    // hitting exactly this race. Handles both.
    await page.locator(`[data-table="${seed.tableId}"]`).click();
    await Promise.race([
      page.locator('#billSheet.is-open').waitFor({ state: 'visible' }),
      page.locator('#productList [data-product]').first().waitFor({ state: 'visible' }),
    ]);
    if (await page.locator('#billSheet.is-open').isVisible()) {
      await page.locator('#btnAddItems').click();
    }
    await expect(page.locator('#productList [data-product]').first()).toBeVisible();

    await expect(page.locator('[data-product]', { hasText: 'E2E Köfte' })).toBeVisible();
    await expect(page.locator('[data-product]', { hasText: 'E2E Izgara Tabağı' })).toBeVisible();

    await page.locator('#productSearch').fill('Izgara');

    // Immediately after typing, the debounce has not settled yet - the old,
    // unfiltered list is still what is on screen.
    await expect(page.locator('[data-product]', { hasText: 'E2E Köfte' })).toBeVisible();

    // Once it settles, the search actually filters, correctly.
    await expect(page.locator('[data-product]', { hasText: 'E2E Köfte' })).toBeHidden({ timeout: 2_000 });
    await expect(page.locator('[data-product]', { hasText: 'E2E Izgara Tabağı' })).toBeVisible();
  });
});
