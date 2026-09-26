import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// V1-WTR-053 / V1-RMD-171 / V1-RMD-185 / V1-RMD-183: the offline retry queue
// is the one place in this app where a bug silently loses an order. Until
// now its only end-to-end proof was a throwaway script that was deleted; these
// are the permanent regression specs. Every test gets its own untouched table
// (seed.offlineTableIds) so no scenario inherits another's order or queue.
const SUBMIT_DRAFT = '**/api/v1/terminals/*/orders/*/submit-draft';
const TABLE_DRAFT = '**/api/v1/terminals/*/orders/table-draft';

const ribbonQueue = (page) => page.locator('#ribbonQueue');

async function startRound(page, tableIndex) {
  await page.locator(`[data-table="${seed.offlineTableIds[tableIndex]}"]`).click();
  await expect(page.locator('#productList [data-product]').first()).toBeVisible();
  await page.locator(`[data-product="${seed.plainProductId}"]`).click();
  await expect(page.locator('#cartCount')).toHaveText('1');
}

async function sendRound(page) {
  await page.locator('#btnSendFromMenu').click();
}

async function goOfflineAndQueue(page, context, tableIndex) {
  await context.setOffline(true);
  await expect(page.locator('#ribbonText')).toContainText('Bağlantı yok');
  await startRound(page, tableIndex);
  await sendRound(page);
  await expect(page.locator('.toast-text', { hasText: 'kuyruğa alındı' })).toBeVisible();
}

async function readQueue(page) {
  return page.evaluate(() => ({
    queue: JSON.parse(localStorage.getItem('alkaros_waiter_offline_queue') || '[]'),
    failed: JSON.parse(localStorage.getItem('alkaros_waiter_failed_orders') || '[]'),
  }));
}

/** Server truth: the order (and its items) the server holds for a table, or null. */
async function serverOrderItemCount(page, tableId) {
  const session = await (await page.request.get('/api/v1/auth/session/current')).json();
  const response = await page.request.get(`/api/v1/terminals/${session.terminalId}/orders/table/${tableId}`);
  if (!response.ok()) return 0;
  const body = await response.json();
  const order = Array.isArray(body) ? body[0] : body;
  return order && order.items ? order.items.length : 0;
}

test.describe('Çevrimdışı sipariş kuyruğu (V1-WTR-053)', () => {
  test('çevrimdışıyken gönderilen tur kuyruğa girer, bağlantı gelince kendiliğinden gönderilir', async ({ page, context }) => {
    await login(page, seed);
    await goOfflineAndQueue(page, context, 0);

    await expect(ribbonQueue(page)).toBeVisible();
    await expect(ribbonQueue(page)).toContainText('1 bekleyen');
    const queued = await readQueue(page);
    expect(queued.queue).toHaveLength(1);
    expect(queued.failed).toHaveLength(0);

    await context.setOffline(false);
    await expect(page.locator('#ribbonText')).toHaveText('Bağlı', { timeout: 10_000 });
    // Otomatik boşaltma `online` olayıyla ANINDA olmalı. Süre bilerek 15 sn'lik
    // geri-deneme zamanlayıcısının (QUEUE_RETRY_MIN_MS) altında tutuldu: aksi
    // halde `online` dinleyicisi bozulsa bile zamanlayıcı turu gönderir ve
    // test yine geçerdi.
    await expect(ribbonQueue(page)).toBeHidden({ timeout: 8_000 });
    expect((await readQueue(page)).queue).toHaveLength(0);
    // Sunucu gerçeği: sipariş gerçekten mutfağa ulaştı.
    await expect.poll(() => serverOrderItemCount(page, seed.offlineTableIds[0]), { timeout: 15_000 }).toBe(1);
  });

  test('flush sırasında 4xx alan tur sessizce silinmez: hatalı listesine taşınır, Sil ve tümünü temizle çalışır', async ({ page, context }) => {
    await login(page, seed);
    await goOfflineAndQueue(page, context, 1);

    await page.route(SUBMIT_DRAFT, (route) => route.fulfill({
      status: 422,
      contentType: 'application/json',
      body: JSON.stringify({ error: { code: 'E2E_REJECTED', message: 'Sipariş reddedildi (test).' } }),
    }));
    await context.setOffline(false);

    // Bekleyen değil, HATALI olarak görünür; içerik kaybolmaz.
    await expect(ribbonQueue(page)).toContainText('1 hatalı', { timeout: 15_000 });
    await expect(ribbonQueue(page)).not.toContainText('bekleyen');
    const state = await readQueue(page);
    expect(state.queue).toHaveLength(0);
    expect(state.failed).toHaveLength(1);
    expect(state.failed[0].items.length).toBeGreaterThan(0);

    await ribbonQueue(page).click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await expect(page.locator('#optionsSheet')).toContainText('OFF-2');
    await expect(page.locator('#optionsSheet')).toContainText('Sipariş reddedildi (test).');

    // Tek tek Sil.
    await page.locator('[data-dismiss-failed]').first().click();
    await expect(page.locator('#optionsSheet')).toContainText('Bekleyen veya hatalı sipariş yok.');
    await expect(ribbonQueue(page)).toBeHidden();
    expect((await readQueue(page)).failed).toHaveLength(0);
  });

  test('429 ve 5xx ile karşılaşan tur kuyrukta KALIR (hatalıya taşınmaz), yeniden denemede gider', async ({ page, context }) => {
    await login(page, seed);
    await goOfflineAndQueue(page, context, 2);

    let mode = 429;
    await page.route(SUBMIT_DRAFT, (route) => route.fulfill({
      status: mode,
      contentType: 'application/json',
      body: JSON.stringify({ error: { code: 'E2E_TEMP', message: 'Geçici.' } }),
    }));

    for (const status of [429, 503]) {
      mode = status;
      await context.setOffline(false);
      await expect(page.locator('#ribbonText')).toHaveText('Bağlı', { timeout: 10_000 });
      // Geçici hata: hâlâ bekleyen, asla hatalı değil.
      await expect(ribbonQueue(page)).toContainText('1 bekleyen');
      await expect(ribbonQueue(page)).not.toContainText('hatalı');
      const state = await readQueue(page);
      expect(state.queue, `status ${status}`).toHaveLength(1);
      expect(state.failed, `status ${status}`).toHaveLength(0);
      await context.setOffline(true);
      await expect(page.locator('#ribbonText')).toContainText('Bağlantı yok');
    }

    // Sunucu düzelince aynı tur kaybolmadan gönderilir.
    await page.unroute(SUBMIT_DRAFT);
    await context.setOffline(false);
    await expect(ribbonQueue(page)).toBeHidden({ timeout: 20_000 });
    await expect.poll(() => serverOrderItemCount(page, seed.offlineTableIds[2]), { timeout: 15_000 }).toBe(1);
  });

  test('sayfa yenilenince kuyruktaki tur korunur ve rozet hemen görünür', async ({ page, context }) => {
    await login(page, seed);
    await goOfflineAndQueue(page, context, 3);

    // Çevrimiçi ama sunucu 503: tur kuyrukta kalsın.
    await page.route(SUBMIT_DRAFT, (route) => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await context.setOffline(false);
    await expect(ribbonQueue(page)).toContainText('1 bekleyen');

    await page.reload();
    await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
    await expect(ribbonQueue(page)).toContainText('1 bekleyen');
    expect((await readQueue(page)).queue).toHaveLength(1);
  });

  test('bozuk localStorage içeriği uygulamayı çökertmez, boş kuyrukla açılır', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('alkaros_waiter_offline_queue', '{bozuk json');
      localStorage.setItem('alkaros_waiter_failed_orders', 'null-değil-dizi');
      localStorage.setItem('alkaros_waiter_drafts_by_table', '[[[');
    });
    const errors = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await login(page, seed);
    await expect(ribbonQueue(page)).toBeHidden();
    expect(errors, `unexpected page errors: ${errors.join(' | ')}`).toEqual([]);
  });

  test('sekme arka plandan dönünce (visibilitychange) bekleyen tur gönderilir', async ({ page, context }) => {
    await login(page, seed);
    await goOfflineAndQueue(page, context, 4);
    await page.route(SUBMIT_DRAFT, (route) => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await context.setOffline(false);
    await expect(ribbonQueue(page)).toContainText('1 bekleyen');

    await page.unroute(SUBMIT_DRAFT);
    // 15 sn'lik geri-deneme zamanlayıcısının altında: gönderimi yapan
    // zamanlayıcı değil, visibilitychange dinleyicisinin kendisi olmalı.
    // V1-RMD-330: çevrimiçi olayının başlattığı gönderim hâlâ sürüyorsa uygulama
    // artık tetiği atlamaz, hatırlar ve o gönderim bitince hemen yeniden dener;
    // bu yüzden tek bir olay yeterlidir.
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    await expect(ribbonQueue(page)).toBeHidden({ timeout: 8_000 });
    await expect.poll(() => serverOrderItemCount(page, seed.offlineTableIds[4]), { timeout: 15_000 }).toBe(1);
  });
});

test.describe('Canlı gönderim dalları (V1-RMD-185)', () => {
  test('çevrimiçiyken sunucu 503 verirse tur kuyruğa alınır, Türkçe uyarı gösterilir', async ({ page }) => {
    await login(page, seed);
    await page.route(TABLE_DRAFT, (route) => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await startRound(page, 5);
    await sendRound(page);
    await expect(page.locator('.toast-text', { hasText: 'Sunucuya ulaşılamadı' })).toBeVisible();
    await expect(ribbonQueue(page)).toContainText('1 bekleyen');
    expect((await readQueue(page)).queue).toHaveLength(1);
  });

  test('çevrimiçiyken 429 tur kuyruğa alır (hatalıya değil), "sunucu yoğun" der', async ({ page }) => {
    await login(page, seed);
    await page.route(TABLE_DRAFT, (route) => route.fulfill({
      status: 429, contentType: 'application/json', body: JSON.stringify({ error: { message: 'Çok fazla istek.' } }),
    }));
    await startRound(page, 6);
    await sendRound(page);
    await expect(page.locator('.toast-text', { hasText: 'Sunucu şu an yoğun' })).toBeVisible();
    const state = await readQueue(page);
    expect(state.queue).toHaveLength(1);
    expect(state.failed).toHaveLength(0);
  });

  test('çevrimiçiyken gerçek bir 4xx reddi kuyruğa girmez: yazılanlar ekranda kalır', async ({ page }) => {
    await login(page, seed);
    await page.route(TABLE_DRAFT, (route) => route.fulfill({
      status: 422, contentType: 'application/json', body: JSON.stringify({ error: { code: 'X', message: 'Ürün artık satışta değil.' } }),
    }));
    await startRound(page, 7);
    await sendRound(page);
    await expect(page.locator('.toast-text', { hasText: 'Ürün artık satışta değil.' })).toBeVisible();
    // Reddedilen tur kuyruğa da hatalıya da girmez, sepet korunur.
    const state = await readQueue(page);
    expect(state.queue).toHaveLength(0);
    expect(state.failed).toHaveLength(0);
    await expect(page.locator('#cartCount')).toHaveText('1');
  });
});

test.describe('Kuyruk önceliği (sortQueueByPriority)', () => {
  test('önce düşük kurs numarası; aynı kursta en eski; 2 dakikayı aşan tur en öne yaşlanır', async ({ page }) => {
    await login(page, seed);
    const order = await page.evaluate(async () => {
      const { sortQueueByPriority } = await import('/js/offline-queue.js');
      const now = Date.parse('2026-01-01T12:00:00Z');
      const iso = (secondsAgo) => new Date(now - secondsAgo * 1000).toISOString();
      const round = (id, course, secondsAgo) => ({
        id, queuedAt: iso(secondsAgo), items: course == null ? [] : [{ courseNumber: course }],
      });
      const queue = [
        round('tatli-3', 3, 30),
        round('baslangic-1-yeni', 1, 10),
        round('baslangic-1-eski', 1, 50),
        round('kurssuz', null, 20),
        round('tatli-yaslanmis', 3, 200), // 200 sn > 120 sn: en öne yaşlanır
      ];
      return sortQueueByPriority(queue, now).map((r) => r.id);
    });
    expect(order).toEqual([
      'tatli-yaslanmis', // yaşlanmış: etkin kurs -1
      'kurssuz',         // kurs yok = 0
      'baslangic-1-eski',
      'baslangic-1-yeni',
      'tatli-3',
    ]);
  });
});
