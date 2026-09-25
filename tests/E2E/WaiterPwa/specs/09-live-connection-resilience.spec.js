import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// V1-RMD-285: the live-update (SignalR) connection used to give up for good
// after five automatic retries, and a failed first start() was swallowed - a
// waiter whose server blipped at page load never got live updates again.
const HUB = '**/hubs/waiter-order-status**';
const PENDING = '**/api/v1/terminals/*/orders/pending';
const ribbonText = (page) => page.locator('#ribbonText');

test.describe('Canlı bağlantı dayanıklılığı (V1-RMD-285)', () => {
  test('sayfa açılırken sunucu canlı bağlantıya cevap vermiyorsa sonradan kendiliğinden bağlanır', async ({ page }) => {
    test.setTimeout(90_000);
    await page.route(HUB, (route) => route.abort());
    await login(page, seed);
    await expect(ribbonText(page)).toContainText('yeniden bağlanıyor');

    let pendingReloads = 0;
    await page.route(PENDING, (route) => { pendingReloads += 1; return route.continue(); });
    await page.unroute(HUB);

    await expect(ribbonText(page)).toHaveText('Bağlı', { timeout: 30_000 });
    // What was missed while the connection was down is reloaded on connect.
    await expect.poll(() => pendingReloads).toBeGreaterThan(0);
  });

  test('bağlantı koptuktan sonra geri gelince canlı bağlantı yeniden kurulur ve bekleyen siparişler yeniden yüklenir', async ({ page, context }) => {
    test.setTimeout(150_000);
    await login(page, seed);
    await expect(ribbonText(page)).toHaveText('Bağlı');

    let pendingReloads = 0;
    await page.route(PENDING, (route) => { pendingReloads += 1; return route.continue(); });

    await context.setOffline(true);
    await expect(ribbonText(page)).toContainText('Bağlantı yok');
    // Offline emulation drops traffic without closing the socket, so the client
    // only notices after SignalR's 30 s server timeout; stay offline past that
    // and past the old ~19 s automatic-reconnect budget.
    await page.waitForTimeout(40_000);
    await expect(ribbonText(page)).toContainText('Bağlantı yok');
    await context.setOffline(false);

    await expect(ribbonText(page)).toHaveText('Bağlı', { timeout: 30_000 });
    await expect.poll(() => pendingReloads, { timeout: 30_000 }).toBeGreaterThan(0);
  });
});
