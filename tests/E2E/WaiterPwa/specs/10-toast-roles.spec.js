import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

// V1-RMD-363 (module-by-module UI audit, 2026-09-27): the WaiterPwa module.
// toast.js's shared #toasts container only ever had aria-live="polite" -
// individual toast nodes carried no role at all, so a screen reader had no
// way to tell an ordinary confirmation apart from a real failure worth
// stopping for. This drives two REAL toasts (a success and the server's
// own 2-minute help-request cooldown as a warning) and checks each gets
// the role toast() now sets.
test.describe('Bildirim (toast) rolleri (V1-RMD-363)', () => {
  test('normal bildirim role="status", uyarı bildirimi role="alert" taşır', async ({ page }) => {
    const seed = readSeed();
    await login(page, seed);
    // secondTableId, not tableId: 03-waiter-actions.spec.js already calls
    // /help-requests once on tableId, and its own 2-minute per-table
    // cooldown (V1-WTR-014) would race the "first call succeeds" assertion
    // below in a full-suite run. secondTableId has never had one.
    // Its own occupied-or-not state still depends on the rest of the suite
    // (an occupied table opens straight onto the bill sheet; a still-empty
    // one opens the menu screen first, and btnHelpRequest lives in the bill
    // sheet's own header) - this handles both.
    await page.locator(`[data-table="${seed.secondTableId}"]`).click();
    // Whichever screen this lands on takes a moment to render - waiting on
    // one specific locator with no fallback is exactly what raced and
    // failed here the first time this test ran against the full suite.
    await Promise.race([
      page.locator('#billSheet.is-open').waitFor({ state: 'visible' }),
      page.locator('#productList [data-product]').first().waitFor({ state: 'visible' }),
    ]);
    const billAlreadyOpen = await page.locator('#billSheet.is-open').isVisible();
    if (!billAlreadyOpen) {
      await page.locator('[data-product]').first().click();
      // Some seeded products carry modifier groups, which routes through the
      // options sheet instead of adding straight to the draft.
      if (await page.locator('#optionsSheet.is-open').isVisible().catch(() => false)) {
        await page.locator('#optionsConfirm').click();
      }
      await page.locator('#btnOpenBill').click();
    }
    await expect(page.locator('#billSheet')).toHaveClass(/is-open/);

    // First call: a real success - the server actually accepts it.
    await page.locator('#btnHelpRequest').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await page.locator('[data-reason]').first().click();
    const firstCall = page.waitForResponse((response) => response.url().includes('/help-requests'));
    await page.locator('#optionsConfirm').click();
    await firstCall;
    const successToast = page.locator('.toast', { hasText: 'yöneticiye iletildi' });
    await expect(successToast).toHaveAttribute('role', 'status');

    // Second call, same table, immediately after: the server's own 2-minute
    // per-table cooldown (V1-WTR-014) answers 429 - a real warning, not a
    // fabricated client-side one.
    await page.locator('#btnHelpRequest').click();
    await page.locator('[data-reason]').first().click();
    const secondCall = page.waitForResponse((response) => response.url().includes('/help-requests'));
    await page.locator('#optionsConfirm').click();
    const response = await secondCall;
    expect(response.status()).toBe(429);
    const warningToast = page.locator('.toast .toast-mark.is-warning').locator('xpath=..');
    await expect(warningToast).toHaveAttribute('role', 'alert');
  });
});
