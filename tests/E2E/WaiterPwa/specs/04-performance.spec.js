import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// Not a load test (one browser, one request at a time) - a real-browser
// timing snapshot of the handful of round trips a waiter actually waits on.
// Useful as a regression trip-wire (a number that suddenly triples between
// runs is worth looking at) more than as an absolute performance claim.
test.describe('Hız ölçümü', () => {
  test('sayfa yükleme ve temel API çağrılarının süresi', async ({ page }) => {
    const timings = {};

    const navStart = Date.now();
    await page.goto('/');
    await page.locator('#loginOverlay').waitFor({ state: 'visible' });
    timings.initialPageLoadMs = Date.now() - navStart;

    const nav = await page.evaluate(() => {
      const [entry] = performance.getEntriesByType('navigation');
      return entry
        ? { domContentLoadedMs: entry.domContentLoadedEventEnd, loadEventMs: entry.loadEventEnd }
        : null;
    });
    timings.navigationTiming = nav;

    const loginStart = Date.now();
    await page.locator('#loginUsername').fill(seed.waiterUsername);
    await page.locator('#loginPassword').fill(seed.waiterPassword);
    const loginResponse = page.waitForResponse((r) => r.url().includes('/auth/login'));
    await page.locator('#loginSubmit').click();
    await loginResponse;
    await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible();
    timings.loginToTablesVisibleMs = Date.now() - loginStart;

    const openStart = Date.now();
    const orderResponse = page.waitForResponse((r) => r.url().includes('/orders/table/'));
    await page.locator(`[data-table="${seed.tableId}"]`).click();
    await orderResponse;
    timings.openTableRoundTripMs = Date.now() - openStart;

    const addStart = Date.now();
    await page.locator(`[data-product="${seed.plainProductId}"]`).click();
    await expect(page.locator('#cartCount')).toHaveText('1');
    timings.quickAddToCartMs = Date.now() - addStart;

    const sendStart = Date.now();
    const submitResponse = page.waitForResponse((r) =>
      r.url().includes('/submit-draft') && r.request().method() === 'POST');
    await page.locator('#btnSendFromMenu').click();
    const submit = await submitResponse;
    timings.tableDraftPlusSubmitDraftRoundTripMs = Date.now() - sendStart;

    console.log('WAITERPWA_TIMINGS ' + JSON.stringify(timings, null, 2));

    expect(submit.status()).toBe(200);
    // Loose trip-wires, not tight SLAs - a real regression looks like
    // seconds, not milliseconds, on a loopback connection with no network
    // latency at all.
    expect(timings.loginToTablesVisibleMs).toBeLessThan(5000);
    expect(timings.openTableRoundTripMs).toBeLessThan(3000);
    expect(timings.tableDraftPlusSubmitDraftRoundTripMs).toBeLessThan(5000);
  });
});
