import { randomUUID } from 'node:crypto';
import { expect } from '@playwright/test';
import { KASA_1_TABLE_ID, KASA_1_TABLE_NUMBER } from './seed.js';

// Helpers for the split-payment specs (07+). Every scenario builds its OWN
// bill through the same real HTTP endpoints cashier-app.js's dispatch and the
// billing bridge use (table-draft -> submit-draft -> send-to-cashier ->
// billing/bills/from-order) - no direct SQL, so what the page under test then
// reads is a bill the production code paths themselves produced.
//
// Each test gets a fresh Playwright BrowserContext, hence a fresh cookie jar;
// logging in here with a brand-new terminalId also gives it a cash-session
// state of its own (sessions are per terminal), so a test that opens a
// session never leaks it into another.

export const SPLIT_PAYMENT_PATH = '/cashier/payments/split-payment/index.html';

/** Real cashier login through the API; the cookie lands in the page's own context. */
export async function loginViaApi(page, seed, username = seed.cashierUsername) {
  const terminalId = randomUUID();
  const response = await page.request.post('/api/v1/auth/login', {
    data: { Username: username, Password: seed.cashierPassword, TerminalId: terminalId },
  });
  expect(response.ok(), `login failed: ${response.status()}`).toBeTruthy();
  const body = await response.json();
  return body.terminalId;
}

async function expectOk(response, what) {
  const text = await response.text();
  expect(response.ok(), `${what} -> ${response.status()}: ${text}`).toBeTruthy();
  return text ? JSON.parse(text) : null;
}

/**
 * Creates a real, payable Bill worth `quantity` x the seeded 100,00 payment
 * product and returns its id. KASA-1 is the one fixed over-the-counter table
 * the vanilla Cashier hardcodes; it is released again (send-to-cashier) exactly
 * like the client's own dispatch does, so consecutive scenarios never collide.
 */
export async function createBill(page, terminalId, seed, { quantity = 1, table = null } = {}) {
  const base = `/api/v1/terminals/${terminalId}`;
  // `table` = a real seeded table ({ tableId, tableNumber }); default is the
  // fixed over-the-counter KASA-1 the vanilla Cashier client hardcodes.
  const tableId = table ? table.tableId : KASA_1_TABLE_ID;
  const tableNumber = table ? table.tableNumber : KASA_1_TABLE_NUMBER;
  const draft = await expectOk(
    await page.request.post(`${base}/orders/table-draft`, {
      data: {
        id: randomUUID(),
        tableId,
        tableNumber,
        items: [{
          id: randomUUID(),
          productId: seed.paymentProductId,
          name: 'E2E Ödeme Ürünü',
          productName: 'E2E Ödeme Ürünü',
          quantity,
          unitPrice: 100,
          specialInstructions: null,
        }],
      },
    }),
    'table-draft',
  );

  await expectOk(
    await page.request.post(`${base}/orders/${draft.orderId}/submit-draft`, {
      data: { orderId: draft.orderId, expectedRowVersion: draft.rowVersion, operationId: `${draft.orderId}:submit` },
    }),
    'submit-draft',
  );
  // Only the over-the-counter KASA-1 is released again (the client's own
  // dispatch does this); a real table stays occupied by its open order.
  if (!table) {
    await expectOk(
      await page.request.post(`${base}/orders/${draft.orderId}/send-to-cashier`, {
        data: { tableId: KASA_1_TABLE_ID },
      }),
      'send-to-cashier',
    );
  }

  const bill = await expectOk(
    await page.request.post(`${base}/billing/bills/from-order/${draft.orderId}`),
    'bills/from-order',
  );
  const billId = bill.billId ?? bill.bill?.id ?? bill.id;
  expect(billId, `no bill id in ${JSON.stringify(bill)}`).toBeTruthy();
  return billId;
}

/** Opens a real cash session for this terminal (the Cash chip is disabled without one). */
export async function openCashSession(page, terminalId, openingBalance = 0) {
  await expectOk(
    await page.request.post(`/api/v1/terminals/${terminalId}/cash-sessions`, {
      data: { OpeningBalance: openingBalance },
    }),
    'cash-sessions open',
  );
}

/** Server truth for a bill's tender summary - what the page itself renders from. */
export async function readTenderSummary(page, terminalId, billId) {
  return expectOk(
    await page.request.get(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`),
    'tenders summary',
  );
}

export async function openSplitPayment(page, billId) {
  await page.goto(`${SPLIT_PAYMENT_PATH}?billId=${billId}`);
}

/** Selects a tender method chip by its Turkish label. */
export async function selectMethod(page, method) {
  await page.locator(`.sp-method-chip[data-method="${method}"]`).click();
}

export async function setAmount(page, amount) {
  await page.locator('#amount-draft').fill(String(amount));
}
