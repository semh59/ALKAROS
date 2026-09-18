import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';

// A minimal, valid 1x1 transparent PNG - the test only needs a real
// image/png upload to exercise the real endpoint end to end, not a specific
// visual (light-vs-dark contrast is a Semih's-own-browser check per this
// task's own Acceptance evidence, this suite cannot take screenshots here).
const ONE_PIXEL_PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

test.describe('Ekran koruyucu yükleme/kaldırma (PosTerminal /settings/screensaver + /display)', () => {
  test('yönetici görsel yükler, müşteri ekranı Idle durumunda gösterir; kaldırınca varsayılan karta döner', async ({ page, context }) => {
    const seed = readSeed();
    // Establishes the session cookie via the real login screen; the
    // screensaver settings route (own standalone login card) reads the
    // same cookie (api.session) and skips straight to "ready" since this
    // seeded user holds catalog.manage among its full permission set.
    await loginCashier(page, seed);

    // CustomerDisplayScreensaverSettings.tsx's own comment: there is no
    // manager-facing "what's currently set" GET - "the current media" it
    // shows is purely this session's own in-memory state from the last
    // upload/remove, never refetched from the server. It must therefore
    // stay on ONE page instance for the whole upload -> remove sequence -
    // a second full navigation back to this route would remount it and
    // lose that state (found by this test itself, first draft).
    const settingsPage = await context.newPage();
    await settingsPage.goto('/settings/screensaver');
    await expect(settingsPage.getByRole('heading', { name: /Ekran koru/i })).toBeVisible({ timeout: 10_000 });

    await settingsPage.setInputFiles('input[type="file"]', {
      name: 'screensaver.png',
      mimeType: 'image/png',
      buffer: Buffer.from(ONE_PIXEL_PNG_BASE64, 'base64'),
    });
    await settingsPage.getByRole('button', { name: /Yükle/ }).click();
    await expect(settingsPage.getByText(/Ekran koruyucu görseli yüklendi\./)).toBeVisible({ timeout: 10_000 });

    // A separate browser context - the customer display is a distinct
    // device/origin in production (finding B-4), never sharing the
    // cashier's own cookies.
    const displayPage = await context.browser().newContext().then((c) => c.newPage());
    await displayPage.goto('/display');
    const pairingCode = displayPage.locator('.pairing-code');
    await expect(pairingCode).toBeVisible({ timeout: 15_000 });
    const code = (await pairingCode.textContent()).trim();
    expect(code).toHaveLength(8);

    // Approve the pairing from the cashier's own dialog (Cashier.tsx),
    // exactly the real operator flow - type the code shown on the display.
    // The trigger button and the dialog's own submit button share the
    // exact same label ("Ekranı eşleştir"), so the submit click must be
    // scoped to the dialog itself.
    await page.goto('/');
    await page.getByRole('button', { name: 'Ekranı eşleştir', exact: true }).click();
    const pairingDialog = page.getByRole('dialog');
    await pairingDialog.getByLabel('Eşleştirme kodu').fill(code);
    await pairingDialog.getByRole('button', { name: 'Ekranı eşleştir' }).click();

    // The display polls /complete every 2s (CustomerDisplay.tsx) and moves
    // to its idle screensaver state once approved; no active order exists
    // on this fresh terminal, so it goes idle immediately.
    await expect(displayPage.locator('.idle-screensaver-image')).toBeVisible({ timeout: 15_000 });
    const imageSrc = await displayPage.locator('.idle-screensaver-image').getAttribute('src');
    expect(imageSrc).toBeTruthy();
    await expect(displayPage.locator('.idle-screen-with-image .display-brand')).toBeVisible();

    // Remove the screensaver on the SAME settings page instance, then
    // force the display's own "fresh entry into Idle" refetch condition
    // (CustomerDisplay.tsx's own comment: it only refetches on a
    // false->true idle transition, not continuously) with a reload.
    await settingsPage.getByRole('button', { name: /Kaldır/ }).click();
    await expect(settingsPage.getByText(/Ekran koruyucu kaldırıldı\./)).toBeVisible({ timeout: 10_000 });

    await displayPage.reload();
    await expect(displayPage.locator('.idle-screensaver-image')).toHaveCount(0, { timeout: 15_000 });
    await expect(displayPage.locator('.idle-screen .display-brand')).toBeVisible();

    await displayPage.context().close();
    await settingsPage.close();
  });
});
