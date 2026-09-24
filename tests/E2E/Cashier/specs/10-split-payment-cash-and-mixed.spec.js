import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import {
  loginViaApi, createBill, openCashSession, openSplitPayment, selectMethod, setAmount, readTenderSummary,
} from '../lib/paymentHelpers.js';

// V13-CSH-004 / V13-PUI-001: Cash goes through its own endpoint (it needs an
// open cash session and mints its own Payment), EFT through the generic one.
// A bill split across both must close only when the SERVER's allocations add
// up - the paid screen is driven by the server-read remaining amount.
const addButton = (page) => page.getByRole('button', { name: 'Ödemeyi Ekle' });
const confirmBox = (page) => page.locator('#eft-confirm');

async function setup(page, quantity = 1) {
  const seed = readSeed();
  const terminalId = await loginViaApi(page, seed);
  await openCashSession(page, terminalId, 500);
  const billId = await createBill(page, terminalId, seed, { quantity });
  await openSplitPayment(page, billId);
  await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
  return { terminalId, billId };
}

test.describe('Hesap Ödeme - Nakit ve karma tahsilat (V13-CSH-004, V13-PUI-001)', () => {
  test('açık kasa oturumunda Nakit seçilebilir ve varsayılandır; tam nakit hesabı kapatır', async ({ page }) => {
    const { terminalId, billId } = await setup(page);

    const cashChip = page.locator('.sp-method-chip[data-method="Cash"]');
    await expect(cashChip).toBeEnabled();
    await expect(cashChip).toHaveClass(/is-active/);
    await expect(cashChip).not.toContainText('kasa kapalı');
    // Nakitte onay kutusu ve not alanı yoktur; düğme doğrudan kullanılabilir.
    await expect(confirmBox(page)).toHaveCount(0);
    await expect(page.locator('#note-draft')).toHaveCount(0);
    await expect(addButton(page)).toBeEnabled();

    await addButton(page).click();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(100);
    expect(summary.remainingAmount).toBe(0);
  });

  test('Nakit + EFT karması yalnız sunucu tahsisleri hesabı tamamlayınca kapanır', async ({ page }) => {
    const { terminalId, billId } = await setup(page, 1);

    await setAmount(page, '40');
    await addButton(page).click();
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toContainText('60,00', { timeout: 10_000 });
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toHaveCount(0);

    await selectMethod(page, 'Eft');
    await confirmBox(page).check();
    await setAmount(page, '60');
    await addButton(page).click();

    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocations).toHaveLength(2);
    expect(summary.allocatedTotal).toBe(100);
    expect(summary.remainingAmount).toBe(0);
  });

  test('Nakit tutarı kalanı aşarsa istek çıkmadan reddedilir ve hesap değişmez', async ({ page }) => {
    const { terminalId, billId } = await setup(page);
    const posts = [];
    page.on('request', (r) => { if (r.method() === 'POST' && /cash-tender|\/tenders\//.test(r.url())) posts.push(r.url()); });

    await setAmount(page, '100.01');
    await addButton(page).click();
    await expect(page.getByText('Tutar kalan tutarı aşamaz.')).toBeVisible();
    expect(posts).toHaveLength(0);
    expect((await readTenderSummary(page, terminalId, billId)).allocations).toHaveLength(0);
  });

  test('bir sekmede kapanan hesap, açık kalan ikinci sekmede yeni tahsilatı sunucudan reddettirir', async ({ page, context }) => {
    const { terminalId, billId } = await setup(page);
    const secondTab = await context.newPage();
    await openSplitPayment(secondTab, billId);
    await expect(secondTab.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    await addButton(page).click();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });

    // İkinci sekme hâlâ eski (ödenmemiş) görüntüyü tutuyor; sunucu artık kalan yok diyor.
    await secondTab.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
    await expect(secondTab.locator('.sp-alert-danger')).toBeVisible({ timeout: 10_000 });
    await expect(secondTab.locator('.sp-alert-danger')).not.toContainText(/Exception|System\.|409|500/);

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(100);
    expect(summary.allocations).toHaveLength(1);
    await secondTab.close();
  });
});
