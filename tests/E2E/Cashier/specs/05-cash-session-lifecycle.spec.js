import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { createBill } from '../lib/paymentHelpers.js';

// V13-PUI-002: the standalone Kasa Oturumu page (src/Clients/Cashier/
// wwwroot/payments/cash-session/**) exercised end to end against the real
// Host + real Postgres this suite already boots - no mocked fetch, the same
// backend V13-CSH-004 wired up.
// V1-RMD-377 (module-by-module UI audit round 2): the counting screen now
// defaults to a kupür (banknote/coin) breakdown - these scenarios test the
// session lifecycle itself, not the counting UI (that has its own spec,
// 28-cash-session-denomination-count.spec.js), so they switch to the
// original single free-typed field first.
async function useManualCount(page) {
  await page.getByRole('button', { name: 'Kupürüm yok, tek tutar gireceğim' }).click();
}

async function loginOnCashSessionPage(page, seed) {
  // 02-stock-badge-and-kitchen-dispatch.spec.js's own precedent: UseDefaultFiles
  // does not resolve a bare directory URL to index.html here, only an explicit
  // filename does - same gap, same workaround.
  await page.goto('/cashier/payments/cash-session/index.html');
  await page.locator('#username').fill(seed.cashierUsername);
  await page.locator('#password').fill(seed.cashierPassword);
  await page.getByRole('button', { name: 'Giriş Yap' }).click();
}

test.describe('Kasa Oturumu (V13-PUI-002)', () => {
  test('açık hesap varken kapanış uyarı verir; onay kutusu işaretlenmeden kapanmaz', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
    const billId = await createBill(page, terminalId, seed);

    // The cashier session from the till screen is already valid here, so the page opens without its own login.
    await page.goto('/cashier/payments/cash-session/index.html');
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('0');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await useManualCount(page);
    await page.locator('#counted-amount').fill('0');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();
    await page.getByRole('button', { name: 'Evet, çekmece boş' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.locator('#unsettled-warning')).toContainText('açık hesap');
    await expect(page.getByRole('button', { name: 'Kasayı Kapat' })).toBeDisabled();

    // The bill is cancelled so later scenarios start clean; the warning on screen was read before that,
    // and the server accepts the acknowledgement either way.
    const cancelled = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/cancel`, { data: { Reason: 'E2E temizlik' } });
    expect(cancelled.ok()).toBe(true);
    await page.getByLabel('Bunları bilerek kapatıyorum').check();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
  });

  test('açılış, nakit giriş, sayım ve sıfır farkla kapatma tam akışı', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('#opening-balance')).toHaveValue('0.00');
    await page.locator('#opening-balance').fill('500');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();

    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await expect(page.locator('.cs-status-pill')).toHaveText(/Açık/);

    // Manuel nakit giriş - banka için çekilen 50 TL'nin ardından geri
    // yatırılan bir tutar senaryosu.
    await page.getByRole('button', { name: 'Nakit Giriş' }).click();
    await page.locator('#movement-amount').fill('50');
    await page.locator('#movement-notes').fill('E2E test - manuel giriş');
    await page.getByRole('button', { name: 'Devam' }).click();
    await expect(page.getByRole('heading', { name: 'Kaydetmeden Önce Kontrol Edin' })).toBeVisible();
    await page.getByRole('button', { name: 'Onayla ve kaydet' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await expect(page.locator('#cs-notice')).toHaveText('Giriş kaydedildi (₺50,00).');
    await expect(page.locator('#movement-list')).toContainText('E2E test - manuel giriş');

    // Sayım: 500 açılış + 50 nakit giriş = 550 beklenen; tam o kadar sayılır.
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
    await useManualCount(page);
    await page.locator('#counted-amount').fill('550');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.getByText('₺550,00').first()).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();

    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('₺0,00').last()).toBeVisible();
  });

  // V1-RMD-343 (independent 2026-09-26 audit, orta seviye bulgu): a parked
  // (beklet) cart had no vardiya (shift) boundary at all - it stayed in
  // localStorage indefinitely, recallable by whoever opened the NEXT shift
  // on this same terminal. Closing the shift is exactly that boundary.
  test('kasa kapatıldığında bekletilen sepetler temizlenir', async ({ page }) => {
    const seed = readSeed();
    await page.goto('/cashier/payments/cash-session/index.html');
    await page.evaluate(() => {
      localStorage.setItem('alkaros_cashier_parked', JSON.stringify([
        { id: 'e2e-parked-1', items: [{ productName: 'Test', quantity: 1, unitPrice: 10 }], parkedAt: '12:00' },
      ]));
    });
    expect(await page.evaluate(() => localStorage.getItem('alkaros_cashier_parked'))).not.toBeNull();

    await page.locator('#username').fill(seed.cashierUsername);
    await page.locator('#password').fill(seed.cashierPassword);
    await page.getByRole('button', { name: 'Giriş Yap' }).click();

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('0');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();

    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await expect(page.getByRole('heading', { name: 'Çekmecedeki Nakdi Sayın' })).toBeVisible();
    await useManualCount(page);
    await page.locator('#counted-amount').fill('0');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();
    await page.getByRole('button', { name: 'Evet, çekmece boş' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();

    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
    expect(await page.evaluate(() => localStorage.getItem('alkaros_cashier_parked'))).toBeNull();
  });

  test('fark için açıklama zorunludur; tolerans aşılırsa yönetici açıklaması istenir ve aynı metin iki yere gider', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);

    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('100');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await useManualCount(page);
    await page.locator('#counted-amount').fill('40');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();

    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.locator('#unsettled-warning')).toHaveCount(0);
    await expect(page.locator('#close-tolerance')).toContainText('₺50,00');
    // Fark var: açıklama olmadan istek hiç gönderilmez.
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByText('fark açıklaması yazın')).toBeVisible();

    // Açıklamayla gönderilir; sunucu farkın tolerans sınırını aştığını 409 ile bildirir
    // (ekran kendi sabit eşiğini kopyalamaz) ve yönetici bloğu tek uyarı olarak açılır.
    await page.locator('#difference-explanation').fill('E2E test - sayım hatası, tutar teyit edildi');
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.locator('#override-reason')).toBeVisible();
    await expect(page.getByText('Fark tolerans sınırını aşıyor')).toHaveCount(1);
    await expect(page.locator('#override-reason')).toHaveValue('E2E test - sayım hatası, tutar teyit edildi');

    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
  });

  // The same movement retried after a network failure replays its key; an edited amount or reason gets a new
  // one, because the server compares direction, amount and the trimmed reason on a replay.
  test('ağ hatasından sonra kasa hareketi: aynı hareket aynı anahtarı, değişen tutar veya açıklama yeni anahtarı kullanır', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('500');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await page.getByRole('button', { name: 'Nakit Giriş' }).click();

    const keys = [];
    await page.route('**/cash-movements', async (route) => {
      if (route.request().method() !== 'POST') return route.continue();
      keys.push(JSON.parse(route.request().postData()).IdempotencyKey);
      return route.abort('failed');
    });
    const confirmFailing = async (count) => {
      await page.getByRole('button', { name: 'Onayla ve kaydet' }).click();
      await expect.poll(() => keys.length, { timeout: 10_000 }).toBe(count);
      await expect(page.getByText('Sunucuya ulaşılamadı.')).toBeVisible();
    };
    const edit = async (amount, notes) => {
      await page.getByRole('button', { name: 'Düzelt' }).click();
      await page.locator('#movement-amount').fill(amount);
      await page.locator('#movement-notes').fill(notes);
      await page.getByRole('button', { name: 'Devam' }).click();
      await expect(page.getByRole('button', { name: 'Onayla ve kaydet' })).toBeVisible();
    };

    await page.locator('#movement-amount').fill('50');
    await page.locator('#movement-notes').fill('E2E test - tekrar');
    await page.getByRole('button', { name: 'Devam' }).click();
    await expect(page.getByRole('button', { name: 'Onayla ve kaydet' })).toBeVisible();
    await confirmFailing(1);
    await confirmFailing(2);
    await edit('70', 'E2E test - tekrar');
    await confirmFailing(3);
    await edit('70', 'E2E test - açıklama düzeltildi');
    await confirmFailing(4);
    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[1]);
    expect(keys[3]).not.toBe(keys[2]);

    await page.unroute('**/cash-movements');
    await page.getByRole('button', { name: 'Onayla ve kaydet' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();
    await expect(page.locator('#movement-list')).toContainText('E2E test - açıklama düzeltildi');

    await page.getByRole('button', { name: 'Sayıma Başla' }).click();
    await useManualCount(page);
    await page.locator('#counted-amount').fill('570');
    await page.getByRole('button', { name: 'Sayımı Kaydet' }).click();
    await expect(page.getByRole('heading', { name: 'Fark Teyidi' })).toBeVisible();
    await expect(page.getByText('₺570,00').first()).toBeVisible();
    await page.getByRole('button', { name: 'Kasayı Kapat' }).click();
    await expect(page.getByRole('heading', { name: 'Kasa Kapatıldı' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('₺0,00').last()).toBeVisible();
  });

  test('nakit çıkışta açıklama zorunludur; eşiğin üstü yönetici onayı bekler ve kasadan düşmez', async ({ page }) => {
    const seed = readSeed();
    await loginOnCashSessionPage(page, seed);
    await expect(page.getByRole('heading', { name: 'Vardiyayı Başlat' })).toBeVisible({ timeout: 15_000 });
    await page.locator('#opening-balance').fill('2000');
    await page.getByRole('button', { name: 'Kasayı Aç' }).click();
    await expect(page.getByRole('heading', { name: /E2E Kasiyer/ })).toBeVisible();

    await page.getByRole('button', { name: 'Nakit Çıkış' }).click();
    await page.locator('#movement-amount').fill('800');
    await page.getByRole('button', { name: 'Devam' }).click();
    await expect(page.getByText('Açıklama zorunludur.')).toBeVisible();

    await page.locator('#movement-notes').fill('E2E test - tedarikçi avansı');
    await page.getByRole('button', { name: 'Devam' }).click();
    await expect(page.locator('#approval-warning')).toContainText('Yönetici onayı gerekecek');
    await page.getByRole('button', { name: 'Onaya gönder' }).click();

    await expect(page.locator('#cs-notice')).toContainText('yönetici onayı bekliyor');
    await expect(page.locator('#pending-requests')).toContainText('E2E test - tedarikçi avansı');
    await expect(page.getByRole('button', { name: 'Sayıma Başla' })).toBeDisabled();
    // The seeded cashier holds every permission, so the own request can be rejected but not approved (self-approval is off).
    await expect(page.locator('[data-request-action="approve"]')).toHaveCount(0);
    await page.locator('[data-request-action="reject"]').click();
    await expect(page.locator('#request-confirm-text')).toContainText('geri alınamaz');
    await page.getByRole('button', { name: 'Evet, reddet' }).click();
    await expect(page.locator('#cs-notice')).toHaveText('Çıkış talebi reddedildi.');
    await expect(page.getByRole('button', { name: 'Sayıma Başla' })).toBeEnabled();

    await page.getByRole('button', { name: 'Nakit Çıkış' }).click();
    await page.locator('#movement-amount').fill('900');
    await page.locator('#movement-notes').fill('E2E test - ikinci avans');
    await page.getByRole('button', { name: 'Devam' }).click();
    await page.getByRole('button', { name: 'Onaya gönder' }).click();
    await expect(page.locator('#pending-requests')).toContainText('E2E test - ikinci avans');
    await page.getByRole('button', { name: 'Talebi geri çek' }).click();
    await expect(page.locator('#cs-notice')).toHaveText('Çıkış talebi geri çekildi.');
  });
});
