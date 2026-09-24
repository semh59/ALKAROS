import { test, expect } from '@playwright/test';
import { login, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();
const PIN = '4321';

// V1-WTR-011 / V1-RMD-173 / V1-RMD-178 / V1-RMD-180: the PIN kiosk lock is the
// only thing stopping a passer-by walking up to an unattended waiter tablet.
// It had zero browser coverage, and its history includes a real security bug
// (Escape and a Bluetooth keyboard's Tab could walk straight through the lock
// overlay). These specs pin the properties that matter: the overlay cannot be
// escaped, wrong/right PINs behave, the server-side lockout hands over to the
// login screen without leaving the app dead, and a stale device flag never
// traps a waiter whose account has no PIN.
const lockOverlay = (page) => page.locator('#lockOverlay');

async function sessionTerminalId(page) {
  return (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
}

/** Sets a real PIN server-side, arms this device, and reloads so state.pinArmed is read from storage. */
async function armPin(page) {
  const terminalId = await sessionTerminalId(page);
  const response = await page.request.post(`/api/v1/auth/pin?terminalId=${terminalId}`, {
    data: { currentPassword: seed.waiterPassword, pin: PIN },
  });
  expect(response.ok(), `set PIN -> ${response.status()}`).toBeTruthy();
  await page.evaluate(() => localStorage.setItem('alkaros_waiter_pin_armed', '1'));
  await page.reload();
  await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
}

/**
 * Clicks the options sheet's confirm button once the self-dismissing toasts
 * (which sit over that button for ~5-6 s) have cleared, so a click is never
 * swallowed by a toast from the previous step.
 */
async function clickConfirm(page) {
  // Waiting each toast out would cost ~6 s per click; the toasts are only
  // transient decoration over the button, so clear them from the DOM.
  await page.evaluate(() => document.querySelectorAll('#toasts .toast').forEach((node) => node.remove()));
  await page.locator('#optionsConfirm').click();
}

/**
 * The app behind an overlay is genuinely usable again: a real click on the
 * profile button opens its sheet. Deliberately not "click the first table" -
 * that opens a menu or a bill depending on what earlier specs left behind.
 */
async function expectBackgroundUsable(page) {
  await page.locator('#btnProfile').click();
  await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
  await page.locator('#optionsClose').click();
  await expect(page.locator('#optionsSheet')).not.toHaveClass(/is-open/);
}

/** Locks through the app's own module instance (same URL the app imported). */
async function lockNow(page) {
  await page.evaluate(async () => (await import('/js/kiosk-lock.js')).lockScreen());
  await expect(lockOverlay(page)).toBeVisible();
}

async function pressPin(page, digits) {
  for (const digit of digits) await page.locator(`#pinKeys [data-pin="${digit}"]`).click();
}

/** Elements Tab reaches that are NOT inside the lock overlay (BODY/HTML = focus left the page, fine). */
async function tabEscapes(page, presses = 25) {
  const escaped = [];
  for (let i = 0; i < presses; i++) {
    await page.keyboard.press('Tab');
    const info = await page.evaluate(() => {
      const active = document.activeElement;
      return { tag: active.tagName, id: active.id, inLock: !!active.closest('#lockOverlay') };
    });
    if (!info.inLock && info.tag !== 'BODY' && info.tag !== 'HTML') escaped.push(info);
  }
  return escaped;
}

test.describe('Ekran kilidi / PIN (V1-WTR-011, V1-RMD-173/178/180)', () => {
  test('PIN kurulumu: boş şifre ve kısa PIN reddedilir, doğru kurulum cihazı silahlandırır, kaldırma geri alır', async ({ page }) => {
    await login(page, seed);
    await page.locator('#btnProfile').click();
    await page.locator('[data-profile="lock"]').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);

    await clickConfirm(page);
    await expect(page.locator('.toast-text', { hasText: 'Şifrenizi girin.' })).toBeVisible();

    await page.locator('[data-pin-password]').fill(seed.waiterPassword);
    await page.locator('[data-pin-code]').fill('12');
    await clickConfirm(page);
    await expect(page.locator('.toast-text', { hasText: 'PIN en az 4 rakam olmalı.' })).toBeVisible();
    await page.locator('[data-pin-code]').fill('12ab');
    await clickConfirm(page);
    await expect(page.locator('.toast-text', { hasText: 'PIN en az 4 rakam olmalı.' }).nth(0)).toBeVisible();

    // Yanlış şifre: sunucu reddeder, sayfa Türkçe mesaj gösterir, cihaz silahlanmaz.
    await page.locator('[data-pin-password]').fill('yanlis-sifre');
    await page.locator('[data-pin-code]').fill(PIN);
    await clickConfirm(page);
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    expect(await page.evaluate(() => localStorage.getItem('alkaros_waiter_pin_armed'))).not.toBe('1');

    await page.locator('[data-pin-password]').fill(seed.waiterPassword);
    await clickConfirm(page);
    await expect(page.locator('.toast-text', { hasText: 'Ekran kilidi kuruldu.' })).toBeVisible();
    expect(await page.evaluate(() => localStorage.getItem('alkaros_waiter_pin_armed'))).toBe('1');

    // Kaldırma da şifre ister ve cihazı silahsızlandırır.
    await page.locator('#btnProfile').click();
    await expect(page.locator('[data-profile="lock"]')).toContainText('Ekranı şimdi kilitle');
    await page.locator('[data-profile="pin-off"]').click();
    await page.locator('[data-pin-password]').fill(seed.waiterPassword);
    await clickConfirm(page);
    await expect(page.locator('.toast-text', { hasText: 'Ekran kilidi kaldırıldı.' })).toBeVisible();
    expect(await page.evaluate(() => localStorage.getItem('alkaros_waiter_pin_armed'))).toBe('0');
  });

  test('tuş takımı: yanlış PIN sıfırlanır ve uyarır, 12 hane sınırı, silme, doğru PIN kilidi açar', async ({ page }) => {
    await login(page, seed);
    await armPin(page);
    await lockNow(page);

    await pressPin(page, '0000');
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(page.locator('#lockSub')).toHaveText('PIN hatalı, tekrar deneyin.');
    await expect(page.locator('#pinDots .is-filled')).toHaveCount(0);
    await expect(lockOverlay(page)).toBeVisible();

    // En fazla 12 hane: 15 tuş basımı 12 dolu nokta bırakır.
    await pressPin(page, '1'.repeat(15));
    await expect(page.locator('#pinDots .is-filled')).toHaveCount(12);
    for (let i = 0; i < 12; i++) await page.locator('#pinKeys [data-pin="del"]').click();
    await expect(page.locator('#pinDots .is-filled')).toHaveCount(0);

    await pressPin(page, PIN);
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(lockOverlay(page)).toBeHidden();
    // Kilit açılınca arka plan yeniden etkileşimli.
    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(false);
    await expectBackgroundUsable(page);
  });

  test('kilit ekranından kaçış yok: Escape hiçbir şeyi kapatmaz, Tab kilit katmanının dışına çıkamaz', async ({ page }) => {
    await login(page, seed);
    await armPin(page);
    await lockNow(page);

    await page.keyboard.press('Escape');
    await expect(lockOverlay(page)).toBeVisible();

    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(true);
    expect(await tabEscapes(page), 'Tab reached an element behind the lock').toEqual([]);
  });

  test('seçenekler sayfası açıkken kilitlenip Escape basmak (V1-RMD-178) kilidi ve arka plan tuzağını bozmaz', async ({ page }) => {
    await login(page, seed);
    await armPin(page);

    await page.locator('#btnProfile').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await lockNow(page);

    await page.keyboard.press('Escape');
    // Kilit hâlâ görünür ve seçenekler sayfası kilit yüzünden kapatılamaz.
    await expect(lockOverlay(page)).toBeVisible();
    expect(await tabEscapes(page), 'Escape released the lock\'s own focus trap').toEqual([]);

    await pressPin(page, PIN);
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(lockOverlay(page)).toBeHidden();
  });

  test('bir sayfa açıkken ekran kilitlenirse PIN tuşları çalışır ve kilit açılınca sayfa yerinde durur', async ({ page }) => {
    await login(page, seed);
    await armPin(page);

    await page.locator('#btnProfile').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    await lockNow(page);
    // Kilit katmanı görünür VE canlı olmalı (alttaki sayfanın tuzağı onu inert bırakmamalı).
    expect(await page.evaluate(() => document.querySelector('#lockOverlay').inert)).toBe(false);

    await pressPin(page, PIN);
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(lockOverlay(page)).toBeHidden();
    // Kilit açıldı: sayfa hâlâ açık ve kullanılabilir (alttaki tuzak geri geldi).
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);
    expect(await page.evaluate(() => document.querySelector('#optionsSheet').inert)).toBe(false);
  });

  test('bir sayfa açıkken oturum biterse giriş ekranı kullanılabilir (inert kalmaz)', async ({ page }) => {
    await login(page, seed);
    await page.locator('#btnProfile').click();
    await expect(page.locator('#optionsSheet')).toHaveClass(/is-open/);

    await page.evaluate(async () => (await import('/js/auth.js')).showLogin());
    await expect(page.locator('#loginOverlay')).toBeVisible();
    expect(await page.evaluate(() => document.querySelector('#loginOverlay').inert)).toBe(false);

    await page.locator('#loginUsername').fill(seed.waiterUsername);
    await page.locator('#loginPassword').fill(seed.waiterPassword);
    await page.locator('#loginSubmit').click();
    await expect(page.locator('#loginOverlay')).toBeHidden({ timeout: 15_000 });
  });

  test('kilit yokken Tab arka plana serbestçe ulaşır (negatif kontrol)', async ({ page }) => {
    await login(page, seed);
    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(false);
    let reachedBackground = false;
    for (let i = 0; i < 15 && !reachedBackground; i++) {
      await page.keyboard.press('Tab');
      reachedBackground = await page.evaluate(() => !!document.activeElement.closest('#screens'));
    }
    expect(reachedBackground).toBe(true);
  });

  test('3 dakika hareketsizlik ekranı kilitler (sahte saatle)', async ({ page }) => {
    await page.clock.install();
    await login(page, seed);
    await armPin(page);
    // Etkileşim boşta sayacını başlatır.
    await page.locator('#tablesGrid').click({ position: { x: 2, y: 2 }, force: true });

    await page.clock.fastForward('02:50');
    await expect(lockOverlay(page)).toBeHidden();
    await page.clock.fastForward('00:20');
    await expect(lockOverlay(page)).toBeVisible();
  });

  test('tam ekrandan çıkış, PIN kuruluyken cihazı kilitler', async ({ page }) => {
    await login(page, seed);
    await armPin(page);
    await expect(lockOverlay(page)).toBeHidden();
    await page.evaluate(() => document.dispatchEvent(new Event('fullscreenchange')));
    await expect(lockOverlay(page)).toBeVisible();
  });

  test('sunucu 423 (çok fazla deneme) verirse giriş ekranına devredilir ve uygulama yeniden kullanılabilir kalır', async ({ page }) => {
    await login(page, seed);
    await armPin(page);
    await lockNow(page);
    await page.route('**/api/v1/auth/unlock**', (route) => route.fulfill({
      status: 423, contentType: 'application/json', body: JSON.stringify({ error: { message: 'Kilitlendi.' } }),
    }));

    await pressPin(page, PIN);
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(page.locator('#lockSub')).toHaveText('Çok fazla deneme. Kullanıcı adı ve şifreyle girin.');
    await expect(page.locator('#loginOverlay')).toBeVisible();

    await page.unroute('**/api/v1/auth/unlock**');
    await page.locator('#loginUsername').fill(seed.waiterUsername);
    await page.locator('#loginPassword').fill(seed.waiterPassword);
    await page.locator('#loginSubmit').click();

    // Çifte tuzak regresyonu (V1-RMD-180): yeniden girişten sonra arka plan
    // sonsuza dek inert kalmamalı; masalar tıklanabilir olmalı.
    await expect(page.locator('#loginOverlay')).toBeHidden({ timeout: 15_000 });
    await expect(lockOverlay(page)).toBeHidden();
    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(false);
    await expectBackgroundUsable(page);
  });

  test('sunucu 409 (hesapta PIN yok) verirse bayat cihaz bayrağı kullanıcıyı kilitte hapsetmez', async ({ page }) => {
    await login(page, seed);
    // Cihaz "PIN var" sanıyor ama sunucuda bu hesapta PIN tanımlı değil.
    await page.evaluate(() => localStorage.setItem('alkaros_waiter_pin_armed', '1'));
    await page.reload();
    await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
    await lockNow(page);
    await page.route('**/api/v1/auth/unlock**', (route) => route.fulfill({
      status: 409, contentType: 'application/json', body: JSON.stringify({ error: { message: 'PIN yok.' } }),
    }));

    await pressPin(page, '1234');
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(lockOverlay(page)).toBeHidden();
    await expect(page.locator('.toast-text', { hasText: 'Bu hesapta PIN tanımlı değil, kilit kaldırıldı.' })).toBeVisible();
    expect(await page.evaluate(() => localStorage.getItem('alkaros_waiter_pin_armed'))).toBe('0');
    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(false);
  });

  test('kilitliyken sayfayı yenilemek kilidi aşmaz; doğru PIN sonrası yenileme kilitsiz açılır', async ({ page }) => {
    await login(page, seed);
    await armPin(page);
    await lockNow(page);

    // Kilit ekranında yenileme: uygulama oturum açık olsa da yeniden kilitli açılmalı.
    await page.reload();
    await expect(lockOverlay(page)).toBeVisible({ timeout: 15_000 });
    expect(await page.evaluate(() => document.querySelector('#screens').inert)).toBe(true);

    await pressPin(page, PIN);
    await page.locator('#pinKeys [data-pin="ok"]').click();
    await expect(lockOverlay(page)).toBeHidden();

    // Kilit açıldıktan sonra yenileme kilitsiz açılır.
    await page.reload();
    await expect(page.locator('#tablesGrid [data-table]').first()).toBeVisible({ timeout: 15_000 });
    await expect(lockOverlay(page)).toBeHidden();
  });
});
