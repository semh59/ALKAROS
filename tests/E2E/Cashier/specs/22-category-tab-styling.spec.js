import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-360 (module-by-module UI audit, 2026-09-27): 641ab882 (2026-08-29) renamed the
// category tab button's class from category-tab-btn to tab-chip in cashier-app.js (both the
// render call and its own .closest('.tab-chip') click-handler selector) but never touched
// cashier-app.css, which still only styled .category-tab-btn - every category tab rendered as
// an unstyled default browser button, with no visible "active" state, for a month, unnoticed by
// two independent audits (2026-09-05, 2026-09-26). Proves the fix with real computed styles:
// a default, unstyled <button> has no border-radius and a UA-default background; the tab-chip
// rule sets a real border-radius, and .active sets a real accent background-color.
test.describe('Kategori sekmeleri gerçekten stillenmiş (vanilla Cashier - /cashier)', () => {
  test('"Tüm Ürünler" sekmesi gerçek CSS alıyor, varsayılan tarayıcı butonu değil', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const allTab = page.locator('.category-tabs .tab-chip', { hasText: 'Tüm Ürünler' });
    await expect(allTab).toBeVisible({ timeout: 15_000 });

    // An un-styled <button> renders with border-radius: 0px in every engine this suite runs on.
    const radius = await allTab.evaluate((el) => getComputedStyle(el).borderRadius);
    expect(radius).not.toBe('0px');

    // "Tüm Ürünler" is selected by default (state.activeCategory === 'all'), so .tab-chip.active
    // must be in effect: a real, non-transparent accent background, not the UA default (button
    // face color, effectively transparent/none as a background-color in getComputedStyle terms).
    await expect(allTab).toHaveClass(/active/);
    const background = await allTab.evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(background).not.toBe('rgba(0, 0, 0, 0)');
    expect(background).not.toBe('transparent');
  });
});
