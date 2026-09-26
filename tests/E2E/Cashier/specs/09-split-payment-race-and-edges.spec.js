import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { readSeed } from '../lib/testHelpers.js';
import {
  loginViaApi, createBill, openSplitPayment, setAmount, readTenderSummary, SPLIT_PAYMENT_PATH,
} from '../lib/paymentHelpers.js';

const addButton = (page) => page.getByRole('button', { name: 'Ödemeyi Ekle' });
const confirmBox = (page) => page.locator('#eft-confirm');

async function payEft(page, amount) {
  await setAmount(page, amount);
  if (!(await confirmBox(page).isChecked())) await confirmBox(page).check();
  await addButton(page).click();
}

test.describe('Hesap Ödeme - yarış, tekrar ve uç durumlar (V13-PUI-001, V1-RMD-258)', () => {
  test('eşzamanlı tahsilatlar hesabı asla aşmaz: kazananlar uygulanır, kaybedenler Türkçe 409 alır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100,00
    const url = `/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`;

    // 4 x 40 = 160 > 100: en fazla 2 tahsilat (80) sığar. Hepsi aynı anda, farklı anahtarlarla.
    const responses = await Promise.all([1, 2, 3, 4].map(() => page.request.post(url, {
      data: { Method: 'Eft', Amount: 40, IdempotencyKey: randomUUID(), Note: null },
    })));

    const statuses = responses.map((r) => r.status());
    const winners = statuses.filter((s) => s >= 200 && s < 300).length;
    expect(winners, `statuses: ${statuses}`).toBe(2);
    for (const response of responses.filter((r) => !r.ok())) {
      // Ham 500 değil: yakalanmış, Türkçe, tipli 409 (V1-RMD-258'in OverAllocation eşlemesi).
      expect(response.status()).toBe(409);
      const body = await response.json();
      expect(body.error.code).toMatch(/^TENDER_/);
      expect(body.error.message).not.toMatch(/Exception|System\.|at .*\.cs/);
    }

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(80);
    expect(summary.remainingAmount).toBe(20);
    expect(summary.allocations).toHaveLength(2);
  });

  test('aynı idempotency anahtarıyla tekrarlanan tahsilat ikinci bir tahsis oluşturmaz', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    const url = `/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`;
    const key = randomUUID();

    const first = await page.request.post(url, { data: { Method: 'Eft', Amount: 60, IdempotencyKey: key, Note: null } });
    const replay = await page.request.post(url, { data: { Method: 'Eft', Amount: 60, IdempotencyKey: key, Note: null } });
    expect(first.ok()).toBeTruthy();
    expect(replay.ok()).toBeTruthy();

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocations).toHaveLength(1);
    expect(summary.allocatedTotal).toBe(60);
  });

  test('V1-RMD-314 (K3): ağ hatası sonrası "tekrar dene" aynı idempotency anahtarını kullanır, mükerrer tahsis oluşturmaz', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100,00
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    // Sunucuya HİÇ ulaşmayan bir ağ hatası simülasyonu: her iki denemede de istek gerçekten bloklanır, biz
    // yalnızca kullanılan idempotency anahtarını gözlemleriz. Fix öncesi bu iki anahtar HER ZAMAN farklıydı
    // (submitTender() her çağrıda crypto.randomUUID() ile taze bir anahtar üretiyordu) - gerçek bir sunucu
    // zaman aşımından sonraki "tekrar dene" bu yüzden yeni bir tahsilat denemesi olarak işlenirdi.
    const capturedKeys = [];
    await page.route(`**/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, async (route) => {
      capturedKeys.push(JSON.parse(route.request().postData()).IdempotencyKey);
      await route.abort('failed');
    });
    await payEft(page, 50);
    await expect(page.getByText('Sunucuya ulaşılamadı.')).toBeVisible({ timeout: 10_000 });
    await addButton(page).click(); // gerçek "tekrar dene" - kullanıcı formu değiştirmeden yeniden tıklıyor
    await expect.poll(() => capturedKeys.length, { timeout: 10_000 }).toBeGreaterThanOrEqual(2);
    expect(new Set(capturedKeys).size).toBe(1);
  });

  test('eşit bölüşümün kuruş kalıntısı gizlenmez: kalan görünür kalır ve elle kapatılır', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 }); // 100,00
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    // 100 / 3 = 33,33: üç eşit tahsilat 99,99 eder, 1 kuruş açıkta kalır.
    await page.locator('#split-count').fill('3');
    await page.getByRole('button', { name: 'Hesapla' }).click();
    await expect(page.locator('#amount-draft')).toHaveValue('33.33');

    await payEft(page, '33.33');
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toContainText('66,67', { timeout: 10_000 });
    await payEft(page, '33.33');
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toContainText('33,34', { timeout: 10_000 });
    await payEft(page, '33.33');

    // Hesap "ödendi" GÖRÜNMEMELİ: 1 kuruş gerçekten açıkta.
    await expect(page.locator('.sp-summary-row.is-remaining .value')).toContainText('0,01', { timeout: 10_000 });
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toHaveCount(0);

    await payEft(page, '0.01');
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.remainingAmount).toBe(0);
    expect(summary.allocations).toHaveLength(4);
  });

  test('tamamen ödenmiş hesap yeniden yüklenince "Hesap Ödendi" gösterir ve tahsilat formu açmaz', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });
    await payEft(page, '100');
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible({ timeout: 10_000 });
    await expect(addButton(page)).toHaveCount(0);
  });

  test('geçersiz durumlar: billId yok, bilinmeyen billId ve oturumsuz erişim güvenli ekranlar gösterir', async ({ page, browser }) => {
    const seed = readSeed();

    // Oturumsuz, taze bir bağlam: giriş kartı, tahsilat formu değil.
    const anonymous = await browser.newContext();
    const anonymousPage = await anonymous.newPage();
    await anonymousPage.goto(`${SPLIT_PAYMENT_PATH}?billId=${randomUUID()}`);
    await expect(anonymousPage.getByRole('heading', { name: 'Kasa Girişi' })).toBeVisible({ timeout: 15_000 });
    await expect(anonymousPage.getByRole('button', { name: 'Ödemeyi Ekle' })).toHaveCount(0);
    await anonymous.close();

    await loginViaApi(page, seed);
    await page.goto(SPLIT_PAYMENT_PATH);
    await expect(page.getByRole('heading', { name: 'Hesap Bulunamadı' })).toBeVisible({ timeout: 15_000 });

    await page.goto(`${SPLIT_PAYMENT_PATH}?billId=${randomUUID()}`);
    await expect(page.getByRole('heading', { name: 'Hesap Bulunamadı' })).toBeVisible({ timeout: 15_000 });
    // Hiçbir ekranda ham HTTP kodu ya da İngilizce hata sızmaz.
    await expect(page.locator('#app')).not.toContainText(/404|Not Found|undefined|\[object/);
  });
});
