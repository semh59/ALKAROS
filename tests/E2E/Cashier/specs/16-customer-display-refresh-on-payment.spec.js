import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-294: CustomerDisplaySnapshotDto is built entirely from orders.orders/order_items, so a bill
// DISCOUNT (billing.bill_adjustments only) changes nothing it reads - confirmed by reading the code, not
// reproduced here, since there is nothing to reproduce. A COMPLETED order's snapshot DOES change (state
// "Completed", message "Teşekkür ederiz."), and nothing told the paired display when a bill's last tender
// completed one. The display also polls every 5s as a fallback, so this spec proves the PUSH specifically by
// asserting the transition well under that window.
test.describe('Müşteri ekranı: ödeme siparişi tamamlayınca anında günceller (V1-RMD-294)', () => {
  test('son tahsilat siparişi tamamlayınca eşleştirilmiş ekran 2 sn içinde "Teşekkür ederiz." gösterir', async ({ page, context }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = await page.evaluate(() => localStorage.getItem('alkaros.terminal-id'));
    const base = `/api/v1/terminals/${terminalId}`;

    // A real walk-in sale through PosTerminal's own DualScreen-backed order flow (Cashier.tsx's
    // startOrder/addItem/submit) - the only path that sets customer_display.terminals.active_order_id, so
    // it is the only order a paired display ever has anything to show for.
    const started = await (await page.request.post(`${base}/orders`, { data: {} })).json();
    const added = await (await page.request.post(`${base}/orders/${started.orderId}/items`, {
      data: { productId: seed.paymentProductId, quantity: 1, expectedRevision: started.revision },
    })).json();
    await page.request.post(`${base}/orders/${started.orderId}/submit`, {
      data: { operationId: randomUUID(), expectedRevision: added.revision },
    });

    // Pair a display to this same terminal - separate browser context, its own cookie jar (finding B-4).
    const displayPage = await context.browser().newContext().then((c) => c.newPage());
    await displayPage.goto('/display');
    const pairingCode = displayPage.locator('.pairing-code');
    await expect(pairingCode).toBeVisible({ timeout: 15_000 });
    const code = (await pairingCode.textContent()).trim();
    await page.getByRole('button', { name: 'Ekranı eşleştir', exact: true }).click();
    const pairingDialog = page.getByRole('dialog');
    await pairingDialog.getByLabel('Eşleştirme kodu').fill(code);
    await pairingDialog.getByRole('button', { name: 'Ekranı eşleştir' }).click();
    await expect(displayPage.getByText('E2E Ödeme Ürünü')).toBeVisible({ timeout: 15_000 });

    // Full payment via a real tender - the same server path split-payment.js itself calls.
    const bill = await (await page.request.post(`${base}/billing/bills/from-order/${started.orderId}`)).json();
    const billId = bill.billId ?? bill.bill?.id ?? bill.id;
    const paidAt = Date.now();
    const tender = await page.request.post(`${base}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'Eft', Amount: 100, IdempotencyKey: randomUUID(), Note: null },
    });
    expect(tender.ok()).toBeTruthy();

    // Well under the display's own 5s poll fallback: proves the push, not the poll.
    await expect(displayPage.getByRole('heading', { name: 'Teşekkür ederiz.' })).toBeVisible({ timeout: 2_000 });
    expect(Date.now() - paidAt).toBeLessThan(2_000);

    await displayPage.context().close();
  });
});
