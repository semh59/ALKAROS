import { test, expect } from '@playwright/test';
import { login, openSeedTable, readSeed } from '../lib/testHelpers.js';

const seed = readSeed();

// One serial flow, one page: party size only settable before the table's
// first round is ever sent, so it has to run first; everything after
// builds on the order that round creates.
test.describe.configure({ mode: 'serial' });

test.describe('Sipariş akışı (kurs, koltuk, ikram, iptal)', () => {
  test.beforeAll(async ({ browser }) => {
    // Shared across this file's tests via a module-level page - Playwright
    // gives each test its own page by default, which would each open a
    // fresh (unauthenticated) context. beforeAll here just documents the
    // ordering; the actual page is created once in the first test and
    // reused by naming the fixture 'page' in every test of this file.
  });

  let sharedPage;
  let sharedContext;

  // Found while root-causing why the full suite's own load-test spec
  // (05-load-and-timing) sometimes hit a client-side timeout only when run
  // after this file, never in isolation: this context was opened here but
  // never closed - every run of this suite leaked one live Chromium
  // context (and 03-waiter-actions.spec.js leaked a second) for the rest
  // of the worker process's lifetime, accumulating real resource pressure
  // by the time 05's own 6-concurrent-context load test ran later in the
  // same run.
  test.afterAll(async () => {
    await sharedContext?.close();
  });

  test('1. masa açılır ve kişi sayısı ilk turdan önce ayarlanır', async ({ browser }) => {
    sharedContext = await browser.newContext();
    sharedPage = await sharedContext.newPage();
    await login(sharedPage, seed);
    await openSeedTable(sharedPage, seed);

    await sharedPage.locator('#btnPartySize').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    await sharedPage.locator('[data-party-step="+"]').click();
    await sharedPage.locator('#optionsConfirm').click();
    await expect(sharedPage.locator('#optionsSheet')).not.toHaveClass(/is-open/);
  });

  test('2. koltuk + kurs + modifier ile ızgara eklenir (1. kurs)', async () => {
    await sharedPage.locator(`[data-product="${seed.modifierProductId}"]`).click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);

    await sharedPage.locator(`[data-seat="${seed.seatOneId}"]`).click();
    await sharedPage.locator('[data-course="1"]').click();
    await sharedPage.locator('[data-modifier]').first().click();
    await sharedPage.locator('#optionsConfirm').click();

    await expect(sharedPage.locator('#optionsSheet')).not.toHaveClass(/is-open/);
    await expect(sharedPage.locator('#cartCount')).toHaveText('1');
  });

  test('3. aynı ürün 2. kurs olarak, farklı koltukta eklenir', async () => {
    await sharedPage.locator(`[data-product="${seed.modifierProductId}"]`).click();
    await sharedPage.locator(`[data-seat="${seed.seatTwoId}"]`).click();
    await sharedPage.locator('[data-course="2"]').click();
    await sharedPage.locator('#optionsConfirm').click();

    await expect(sharedPage.locator('#cartCount')).toHaveText('2');
  });

  test('4. kurssuz bir ürün (Köfte) hızlı eklenir', async () => {
    await sharedPage.locator(`[data-product="${seed.plainProductId}"]`).click();
    await expect(sharedPage.locator('#cartCount')).toHaveText('3');
  });

  test('5. tur mutfağa gönderilir', async () => {
    const submitResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/submit-draft') && response.request().method() === 'POST');
    await sharedPage.locator('#btnSendFromMenu').click();
    const response = await submitResponse;
    expect(response.status()).toBe(200);

    // A successful send returns to the tables screen.
    await expect(sharedPage.locator('#tablesScreen')).toHaveAttribute('data-state', 'on');
  });

  test('6. masa tekrar açılınca 1. kurs mutfakta, 2. kurs bekletiliyor görünür', async () => {
    await sharedPage.locator(`[data-table="${seed.tableId}"]`).click();
    await expect(sharedPage.locator('#billSheet')).toHaveClass(/is-open/);

    const courseOneLine = sharedPage.locator('.line', { hasText: '1. kurs' });
    const courseTwoLine = sharedPage.locator('.line', { hasText: '2. kurs' });
    await expect(courseOneLine.locator('.kitchen-state')).toContainText('Mutfakta');
    await expect(courseTwoLine.locator('.kitchen-state')).toContainText('Bekletiliyor');
    // The held course carries its own fire button; the sent one does not.
    await expect(courseTwoLine.locator('[data-fire-course]')).toBeVisible();
    await expect(courseOneLine.locator('[data-fire-course]')).toHaveCount(0);
  });

  test('7. 2. kurs ateşlenince mutfakta durumuna geçer', async () => {
    const fireResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/fire-course') && response.request().method() === 'POST');
    await sharedPage.locator('.line', { hasText: '2. kurs' }).locator('[data-fire-course]').click();
    const response = await fireResponse;
    expect(response.status()).toBe(200);

    const courseTwoLine = sharedPage.locator('.line', { hasText: '2. kurs' });
    await expect(courseTwoLine.locator('.kitchen-state')).toContainText('Mutfakta');
    await expect(courseTwoLine.locator('[data-fire-course]')).toHaveCount(0);
  });

  test('8. gönderilmiş bir kalem ikram edilebilir', async () => {
    const kofteLine = sharedPage.locator('.line', { hasText: 'E2E Köfte' });
    await kofteLine.locator('[data-comp]').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    await sharedPage.locator('[data-reason]').first().click();
    const compResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/comp') && response.request().method() === 'POST');
    await sharedPage.locator('#optionsConfirm').click();
    const response = await compResponse;
    expect(response.status()).toBe(200);
    // A comp'd line stays on the bill (it was still served, just free) -
    // activeItems() only ever drops Cancelled/Waste lines, never
    // Complimentary ones. It just no longer offers another comp/void.
    const kofteLineAfter = sharedPage.locator('.line', { hasText: 'E2E Köfte' });
    await expect(kofteLineAfter).toHaveCount(1);
    await expect(kofteLineAfter.locator('.line-total')).toHaveText('₺0,00');
    await expect(kofteLineAfter.locator('[data-comp]')).toHaveCount(0);
  });

  test('9. mutfağa gönderilmiş bir kalem için iptal isteği', async () => {
    const courseOneLine = sharedPage.locator('.line', { hasText: '1. kurs' });
    await courseOneLine.locator('[data-void-sent]').click();
    await expect(sharedPage.locator('#optionsSheet')).toHaveClass(/is-open/);
    await sharedPage.locator('[data-reason]').first().click();
    const voidResponse = sharedPage.waitForResponse((response) =>
      response.url().includes('/void-sent') && response.request().method() === 'POST');
    await sharedPage.locator('#optionsConfirm').click();
    const response = await voidResponse;
    expect(response.status()).toBe(200);
    await expect(sharedPage.locator('.line', { hasText: '1. kurs' })).toHaveCount(0);
  });
});
