import { randomUUID } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openCashSession, openSplitPayment, readTenderSummary } from '../lib/paymentHelpers.js';

// V1-RMD-443: the customer account flow end to end on the real host - a customer is added and given a credit limit
// on the Cari Hesaplar page, a bill is written to their account from the payment screen, and part of the debt is
// collected in cash with a receipt.
const CUSTOMER_ACCOUNTS_PATH = '/cashier/payments/customer-accounts/index.html';

async function addCustomer(page, name, phone) {
  await page.goto(CUSTOMER_ACCOUNTS_PATH);
  await expect(page.getByRole('heading', { name: 'Cari Hesaplar' })).toBeVisible({ timeout: 15_000 });
  await page.getByLabel('Yeni müşterinin adı').fill(name);
  if (phone) await page.getByLabel('Yeni müşterinin telefonu (isteğe bağlı)').fill(phone);
  await page.getByRole('button', { name: 'Müşteri ekle' }).click();
  await expect(page.getByRole('heading', { name })).toBeVisible({ timeout: 10_000 });
}

async function writeBillToAccount(page, billId, customerName) {
  await openSplitPayment(page, billId);
  await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
  await page.locator('.sp-method-chip[data-method="Account"]').click();
  await page.getByLabel('Müşteri ara (ad veya telefon)').fill(customerName);
  await page.getByRole('button', { name: 'Ara' }).click();
  await page.getByRole('radio', { name: new RegExp(customerName) }).click();
  await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();
}

test.describe('Cari hesap: müşteri, kredi limiti, hesaba yaz ve nakit tahsilat (V1-RMD-443)', () => {
  test('limitli müşteriye hesaba yazılan adisyon kapanır; borcun bir kısmı nakit tahsil edilip makbuz verilir', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    await openCashSession(page, terminalId, 500);
    const name = `E2E Cari ${randomUUID().slice(0, 6)}`;

    await addCustomer(page, name, '0532 111 22 33');
    await expect(page.getByText('*******2233').first()).toBeVisible();
    await page.getByLabel('Kredi limiti (TL)').fill('500');
    await page.getByLabel('Vade (gün, boş bırakılırsa vade takibi yok)').fill('30');
    await page.getByRole('button', { name: 'Kaydet', exact: true }).click();
    await expect(page.getByText('Kredi limiti kaydedildi.')).toBeVisible({ timeout: 10_000 });

    const billId = await createBill(page, terminalId, seed);
    await writeBillToAccount(page, billId, name);
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
    expect((await readTenderSummary(page, terminalId, billId)).remainingAmount).toBe(0);

    await page.goto(CUSTOMER_ACCOUNTS_PATH);
    await page.getByRole('button', { name: new RegExp(name) }).click();
    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(page.locator('.sp-summary-row.is-total .value')).toContainText('100,00');
    await expect(page.getByRole('cell', { name: 'Hesaba yazılan adisyon' })).toBeVisible();

    await page.getByLabel('Tahsil edilecek tutar').fill('60');
    await page.getByRole('button', { name: 'Tahsil et' }).click();
    await expect(page.getByText(/Tahsil edildi: .*60,00.*makbuz CT-\d{8}.*kalan borç .*40,00/)).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('cell', { name: 'Tahsilat' })).toBeVisible();
  });

  test('kredi limiti olmayan müşteriye hesaba yazma sunucunun Türkçe gerekçesiyle reddedilir; hesap açık kalır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const name = `E2E Limitsiz ${randomUUID().slice(0, 6)}`;
    await addCustomer(page, name, null);
    const billId = await createBill(page, terminalId, seed);

    await writeBillToAccount(page, billId, name);

    await expect(page.getByRole('alert')).toContainText('Müşteri için kredi limiti tanımlanmadığından cari hesaba borç yazılamaz.');
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toHaveCount(0);
    expect((await readTenderSummary(page, terminalId, billId)).allocations).toHaveLength(0);
  });

  // V1-RMD-453: the invoice tax identity is entered here, checked by the server, and only ever shown masked.
  test('şirket müşterisi VKN ve vergi dairesiyle eklenir; hatalı numara Türkçe gerekçeyle reddedilir; kayıt maskeli görünür', async ({ page }) => {
    const seed = readSeed();
    await loginViaApi(page, seed);
    const name = `E2E Şirket ${randomUUID().slice(0, 6)}`;
    await page.goto(CUSTOMER_ACCOUNTS_PATH);
    await expect(page.getByRole('heading', { name: 'Cari Hesaplar' })).toBeVisible({ timeout: 15_000 });

    await page.getByLabel('Yeni müşterinin adı').fill(name);
    await page.getByLabel('Yeni müşterinin vergi kimlik türü').selectOption('Vkn');
    await page.getByLabel('Yeni müşterinin vergi kimlik numarası').fill('1234567891');
    await page.getByLabel('Yeni müşterinin vergi dairesi').fill('Kadıköy');
    await page.getByRole('button', { name: 'Müşteri ekle' }).click();
    await expect(page.getByRole('alert')).toContainText('Vergi kimlik numarası geçersiz; rakamları kontrol edin.');
    await expect(page.getByRole('heading', { name })).toHaveCount(0);

    await page.getByLabel('Yeni müşterinin vergi kimlik numarası').fill('1234567890');
    await page.getByRole('button', { name: 'Müşteri ekle' }).click();
    await expect(page.getByRole('heading', { name })).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('#ca-tax-identity')).toHaveText('VKN *******890 · Kadıköy V.D.');
    await expect(page.getByRole('button', { name: new RegExp(name) })).toContainText('VKN *******890');
    await expect(page.locator('body')).not.toContainText('1234567890');

    await page.getByLabel('Müşterinin vergi kimlik türü', { exact: true }).selectOption('Tckn');
    await page.getByLabel('Müşterinin T.C. kimlik numarası', { exact: true }).fill('10000000146');
    await page.getByLabel('Müşterinin vergi dairesi', { exact: true }).fill('');
    await page.getByRole('button', { name: 'Vergi kimliğini kaydet' }).click();
    await expect(page.getByText('Vergi kimliği kaydedildi.')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('#ca-tax-identity')).toHaveText('TCKN ********146');
  });
});
