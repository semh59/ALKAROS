import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// V1-RMD-293: kitchen.print_jobs (Failed/DeadLetter) had no client at all - a stuck physical print was
// invisible to kitchen staff and the cashier alike. This suite's environment seeds no kitchen printer at all
// (BridgeUnprintedTicketsAsync only ever creates a print job per printer ACTIVELY registered at a ticket's
// station), so a real Failed/DeadLetter job cannot be produced here without seeding one - out of this task's
// Owned surface, and the same "gerçek cihaz doğrulaması" the task itself already puts out of scope. What this
// spec proves instead, against the real Host: the new /print-jobs fan-out actually runs for a real ticket and
// the panel correctly reports no failures rather than silently omitting the check.
test.describe('Mutfak: yazdırma sorunları paneli (V1-RMD-293)', () => {
  test('gerçek bir ticket için print-jobs sorgulanır ve sorun yokken bunu söyler', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    await page.goto('/cashier/index.html');

    const requestedTicketIds = [];
    page.on('request', (request) => {
      const url = request.url();
      if (url.includes('/print-jobs?ticketId=')) requestedTicketIds.push(new URL(url).searchParams.get('ticketId'));
    });

    await page.locator(`.pos-product-card[data-product-id="${seed.normalStockProductId}"]`).click();
    await expect(page.locator('.ticket-row .item-title')).toHaveText('E2E Kasa Normal Stok');
    await page.locator('#btnDispatchOrder').click();
    await expect(page.locator('#toastRegion .toast--success[role="status"]')).toContainText('Sipariş mutfağa iletildi.');

    await page.goto('/kitchen');
    await expect(page.getByText('Yazdırma sorunları')).toBeVisible({ timeout: 15_000 });

    // The real /print-jobs?ticketId=<the real dispatched ticket> call happened - not a hardcoded empty panel.
    await expect.poll(() => requestedTicketIds.length, { timeout: 10_000 }).toBeGreaterThan(0);
    await expect(page.getByText('Bekleyen yazdırma sorunu yok.')).toBeVisible();
    const stat = page.locator('.kitchen-workspace__stats').getByText('Yazdırma sorunu').locator('..');
    await expect(stat).toContainText('0');
  });
});
