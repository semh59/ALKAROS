import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

/**
 * Adds one item (the modifier-bearing product) for a specific seat through
 * the full options sheet - what a waiter actually taps for each guest at
 * the table (not a blind quick-add), so this timing reflects real UI steps:
 * open sheet, pick seat, confirm.
 */
async function addSeatedItem(page, seatId) {
  await page.locator(`[data-product="${seed.modifierProductId}"]`).click();
  await page.locator(`[data-seat="${seatId}"]`).click();
  await page.locator('#optionsConfirm').click();
}

test.describe('Hız: gerçek 4 kişilik masa senaryosu', () => {
  test('4 misafirin siparişi girilip mutfağa gönderilene kadar geçen süre', async ({ page }) => {
    await login(page, seed);

    const start = Date.now();

    await page.locator(`[data-table="${seed.timingTableId}"]`).click();
    await expect(page.locator('#productList [data-product]').first()).toBeVisible();

    // The waiter confirms the party size first, same as a real greeting.
    await page.locator('#btnPartySize').click();
    for (let i = 0; i < 3; i++) await page.locator('[data-party-step="+"]').click(); // 2 -> 5, then back one
    await page.locator('[data-party-step="-"]').click(); // settle on 4
    await page.locator('#optionsConfirm').click();

    // Each of the 4 guests orders the same main, to their own seat - a
    // common real order, and the one shape this app can actually assign a
    // seat to (only a modifier-bearing product opens the seat picker at
    // all; a plain quick-add product never does).
    for (const seatId of seed.timingSeatIds) {
      await addSeatedItem(page, seatId);
    }
    await expect(page.locator('#cartCount')).toHaveText('4');

    const submitResponse = page.waitForResponse((r) =>
      r.url().includes('/submit-draft') && r.request().method() === 'POST');
    await page.locator('#btnSendFromMenu').click();
    const response = await submitResponse;
    expect(response.status()).toBe(200);

    const totalMs = Date.now() - start;
    console.log(`FOUR_TOP_ORDER_TOTAL_MS ${totalMs}`);
    console.log(`FOUR_TOP_ORDER_TOTAL_SECONDS ${(totalMs / 1000).toFixed(2)}`);

    // This is scripted, automated clicking - a real waiter reading the
    // screen, deciding, and tapping takes meaningfully longer per item than
    // Playwright does. This number is a SYSTEM floor (network + server +
    // render time with zero human decision time), not a claim about how
    // fast an actual person could do this.
    expect(totalMs).toBeLessThan(10_000);
  });
});

test.describe('Yük testi: eşzamanlı masalar', () => {
  test(`${seed.loadTableIds.length} garson aynı anda sipariş girip gönderir`, async ({ browser }) => {
    const results = await Promise.all(seed.loadTableIds.map(async (tableId, index) => {
      const context = await browser.newContext();
      const page = await context.newPage();
      const start = Date.now();
      try {
        await login(page, seed);
        const loginDoneMs = Date.now() - start;

        await page.locator(`[data-table="${tableId}"]`).click();
        await expect(page.locator('#productList [data-product]').first()).toBeVisible();
        const tableOpenMs = Date.now() - start;

        // A simple two-line order (no seat picker - these tables are
        // seatless on purpose, concurrency is the point here, not variety):
        // 3x plain product, 1x the modifier product.
        await page.locator(`[data-product="${seed.plainProductId}"]`).click();
        await page.locator(`[data-product="${seed.plainProductId}"]`).click();
        await page.locator(`[data-product="${seed.plainProductId}"]`).click();
        await page.locator(`[data-product="${seed.modifierProductId}"]`).click();
        await page.locator('#optionsConfirm').click();
        await expect(page.locator('#cartCount')).toHaveText('4');

        const submitResponse = page.waitForResponse((r) =>
          r.url().includes('/submit-draft') && r.request().method() === 'POST');
        await page.locator('#btnSendFromMenu').click();
        const response = await submitResponse;
        const totalMs = Date.now() - start;

        return { index, ok: response.status() === 200, status: response.status(), loginDoneMs, tableOpenMs, totalMs };
      } catch (error) {
        return { index, ok: false, error: error.message, totalMs: Date.now() - start };
      } finally {
        await context.close();
      }
    }));

    console.log('LOAD_TEST_RESULTS ' + JSON.stringify(results, null, 2));

    const totals = results.map((r) => r.totalMs).sort((a, b) => a - b);
    const succeeded = results.filter((r) => r.ok);
    const stats = {
      concurrentWaiters: results.length,
      succeeded: succeeded.length,
      failed: results.length - succeeded.length,
      minMs: totals[0],
      maxMs: totals[totals.length - 1],
      avgMs: Math.round(totals.reduce((a, b) => a + b, 0) / totals.length),
      p95Ms: totals[Math.floor(totals.length * 0.95)] ?? totals[totals.length - 1],
    };
    console.log('LOAD_TEST_STATS ' + JSON.stringify(stats, null, 2));

    expect(stats.failed, `Some concurrent orders failed: ${JSON.stringify(results.filter((r) => !r.ok))}`).toBe(0);
  });
});
