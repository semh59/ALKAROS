import { test, expect } from '@playwright/test';
import { readSeed, loginCashier, startOrder } from '../lib/testHelpers.js';

test.describe('İkram sonrası hesap görünümü (PosTerminal Cashier.tsx)', () => {
  test('ürün ikram edilir; gerçek fiyatıyla + ayrı bir indirim satırı olarak görünür', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await startOrder(page);

    await page.getByRole('button', { name: /E2E Kasa Normal Stok/ }).click();
    await expect(page.locator('.ticket-line')).toHaveCount(1);
    const originalLineTotalText = await page.locator('.ticket-line .line-total').textContent();

    await page.getByRole('button', { name: 'Siparişi gönder' }).click();
    // A toast ("Sipariş gönderildi. Yeni satış açabilirsiniz.") and the
    // ticket panel's own submitted-state label share this prefix - scope
    // to the exact submitted-state text.
    await expect(page.getByText('Sipariş gönderildi', { exact: true })).toBeVisible({ timeout: 10_000 });

    // The comp action itself has no button in this client (V1-BIL-005's
    // comp endpoint is exercised by WaiterPwa, not Cashier - confirmed by
    // grep: PosTerminal's api.ts never calls .../comp). This scenario's own
    // job (per V1-CUI-011's Goal) is verifying the Kasa BILL VIEW renders a
    // completed comp correctly (V1-RMD-228's fix), so the comp itself is
    // applied the same way an authorized actor's request reaches the
    // server: a real HTTP call, reusing this same authenticated session's
    // cookie (page.request shares the browser context's cookie jar).
    const terminalId = await page.evaluate(() => localStorage.getItem('alkaros.terminal-id'));
    const activeOrder = await page.request
      .get(`/api/v1/terminals/${terminalId}/orders/active`)
      .then((r) => r.json());
    expect(activeOrder.lines).toHaveLength(1);
    const item = activeOrder.lines[0];

    const compResponse = await page.request.post(
      `/api/v1/terminals/${terminalId}/orders/${activeOrder.orderId}/items/${item.itemId}/comp`,
      {
        data: {
          idempotencyKey: `e2e-comp-${item.itemId}`,
          expectedRowVersion: activeOrder.revision,
          reasonCode: 'CustomerSatisfaction',
          notes: 'E2E suite - V1-RMD-228 bill-view regression coverage',
        },
      },
    );
    expect(compResponse.ok()).toBeTruthy();

    await page.reload();
    await expect(page.locator('.ticket-line')).toHaveCount(1);
    // V1-RMD-228: the comped line still shows its real price (a zero/
    // invisible line would be the bug this regression coverage exists for)
    // ...
    await expect(page.locator('.ticket-line .line-total')).toHaveText(originalLineTotalText);
    // ...and the discount is surfaced separately, as its own aggregate line.
    await expect(page.locator('.ticket-totals').getByText('İndirim')).toBeVisible();
    const discountRow = page.locator('.ticket-totals div', { hasText: 'İndirim' });
    await expect(discountRow).toContainText('−');
  });
});
