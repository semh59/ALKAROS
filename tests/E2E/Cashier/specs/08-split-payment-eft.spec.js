import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import {
  loginViaApi, createBill, openSplitPayment, selectMethod, setAmount, readTenderSummary,
} from '../lib/paymentHelpers.js';

// V13-PAY-005 / V13-PUI-004: EFT/Havale is a cashier-declared tender with no
// provider behind it. The screen must (1) refuse to add it until the cashier
// confirms having seen the money in the bank statement, (2) never show or
// compute change, (3) work with no cash session at all, and (4) keep the
// keyboard user's place across the full re-renders this page does.
async function setup(page, quantity = 1) {
  const seed = readSeed();
  const terminalId = await loginViaApi(page, seed);
  const billId = await createBill(page, terminalId, seed, { quantity });
  const tenderPosts = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().includes('/tenders/')) tenderPosts.push(request.url());
  });
  await openSplitPayment(page, billId);
  await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
  return { terminalId, billId, tenderPosts };
}

const addButton = (page) => page.getByRole('button', { name: 'Ödemeyi Ekle' });
const confirmBox = (page) => page.locator('#eft-confirm');

test.describe('Hesap Ödeme - EFT/Havale (V13-PAY-005, V13-PUI-004)', () => {
  test('onay kutusu işaretlenmeden EFT eklenemez; işaretlenince tam tutar hesabı kapatır, para üstü yoktur', async ({ page }) => {
    const { terminalId, billId, tenderPosts } = await setup(page);

    // Kasa oturumu yok: EFT yine de otomatik seçili ve kullanılabilir olmalı.
    await expect(page.locator('.sp-method-chip[data-method="Eft"]')).toHaveClass(/is-active/);
    await expect(page.locator('.sp-method-chip[data-method="Eft"]')).toBeEnabled();

    await expect(confirmBox(page)).not.toBeChecked();
    await expect(addButton(page)).toBeDisabled();
    // Gerçek tıklama girişimi (yalnız DOM özniteliği değil): hiçbir istek çıkmamalı.
    await addButton(page).click({ force: true, timeout: 2_000 }).catch(() => {});
    await page.waitForTimeout(400);
    expect(tenderPosts).toHaveLength(0);
    expect((await readTenderSummary(page, terminalId, billId)).allocations).toHaveLength(0);

    await confirmBox(page).check();
    await expect(addButton(page)).toBeEnabled();
    await addButton(page).click();

    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
    // Para üstü kavramı EFT'de hiçbir yerde yok.
    await expect(page.getByText(/para üstü/i)).toHaveCount(0);

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(100);
    expect(summary.remainingAmount).toBe(0);
    expect(summary.allocations).toHaveLength(1);
  });

  test('onay yöntem değişince ve her başarılı tahsilattan sonra sıfırlanır; kısmi EFT kalanı düşürür', async ({ page }) => {
    const { terminalId, billId } = await setup(page);

    await confirmBox(page).check();
    await selectMethod(page, 'BankCard');
    await selectMethod(page, 'Eft');
    await expect(confirmBox(page)).not.toBeChecked();
    await expect(addButton(page)).toBeDisabled();

    await confirmBox(page).check();
    await setAmount(page, '30');
    await addButton(page).click();

    await expect(page.locator('.sp-summary-row.is-remaining .value')).toContainText('70,00', { timeout: 10_000 });
    // Bir sonraki EFT tahsilatı kendi onayını taze ister.
    await expect(confirmBox(page)).not.toBeChecked();
    await expect(addButton(page)).toBeDisabled();

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(30);
    expect(summary.remainingAmount).toBe(70);
  });

  test('klavye odağı yöntem seçimi ve onay kutusu yeniden çizimlerinde kaybolmaz', async ({ page }) => {
    await setup(page);
    const activeInfo = () => page.evaluate(() => {
      const el = document.activeElement;
      return { tag: el && el.tagName, method: el && el.getAttribute && el.getAttribute('data-method'), id: el && el.id };
    });

    await page.locator('.sp-method-chip[data-method="BankCard"]').focus();
    await page.keyboard.press('Enter');
    expect(await activeInfo()).toMatchObject({ tag: 'BUTTON', method: 'BankCard' });

    await page.locator('.sp-method-chip[data-method="Eft"]').focus();
    await page.keyboard.press('Enter');
    expect(await activeInfo()).toMatchObject({ tag: 'BUTTON', method: 'Eft' });

    await confirmBox(page).focus();
    await page.keyboard.press('Space');
    await expect(confirmBox(page)).toBeChecked();
    expect(await activeInfo()).toMatchObject({ tag: 'INPUT', id: 'eft-confirm' });
  });

  test('not alanına yazılan öznitelik/HTML enjeksiyonu çalışmaz, yeniden çizimde olduğu gibi korunur', async ({ page }) => {
    await setup(page);
    let dialogSeen = false;
    page.on('dialog', async (dialog) => { dialogSeen = true; await dialog.dismiss(); });

    const payload = '" onmouseover="window.__xss=1" x="<img src=x onerror=alert(1)>';
    await page.locator('#note-draft').fill(payload);
    // Onay kutusu değişimi tüm kartı yeniden çizer; not değeri kaçışlanarak geri yazılmalı.
    await confirmBox(page).check();

    const note = page.locator('#note-draft');
    await expect(note).toHaveValue(payload);
    expect(await note.getAttribute('onmouseover')).toBeNull();
    await note.hover();
    expect(await page.evaluate(() => window.__xss)).toBeUndefined();
    expect(dialogSeen).toBe(false);
    await expect(page.locator('img[src="x"]')).toHaveCount(0);
  });

  test('sınır değerler: kalanı bir kuruş aşan, sıfır ve boş tutar istek çıkmadan reddedilir', async ({ page }) => {
    const { tenderPosts } = await setup(page);
    await confirmBox(page).check();

    await setAmount(page, '100.01');
    await addButton(page).click();
    await expect(page.getByText('Tutar kalan tutarı aşamaz.')).toBeVisible();

    await setAmount(page, '0');
    await confirmBox(page).check().catch(() => {});
    if (!(await confirmBox(page).isChecked())) await confirmBox(page).check();
    await addButton(page).click();
    await expect(page.getByText('Tutar sıfırdan büyük olmalı.')).toBeVisible();

    await setAmount(page, '');
    if (!(await confirmBox(page).isChecked())) await confirmBox(page).check();
    await addButton(page).click();
    await expect(page.getByText('Tutar sıfırdan büyük olmalı.')).toBeVisible();

    expect(tenderPosts).toHaveLength(0);
  });
});
