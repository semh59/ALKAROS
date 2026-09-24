import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import {
  loginViaApi, createBill, openSplitPayment, selectMethod, setAmount, readTenderSummary,
} from '../lib/paymentHelpers.js';

// V13-PUI-001 / V13-PAY-003 / V1-RMD-258 / V13-RMD-002: the split-payment
// screen's single most safety-critical property. No real Token/Beko terminal
// exists yet, so the registered BankCard handler is an honest placeholder that
// must ALWAYS answer "needs manual reconciliation" - never a fabricated
// approval or decline - and once a card attempt is unresolved the bill must
// refuse every further tender, surviving a page reload (server-derived lock,
// not this page's own memory).
test.describe('Hesap Ödeme - kart tahsilatı kilidi (V13-PUI-001, V1-RMD-258)', () => {
  test('kart tahsilatı asla onaylanmış görünmez, hesap kapanmaz, kilit yenilemede korunur', async ({ page, context }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });

    await openSplitPayment(page, billId);
    await expect(page.getByRole('heading', { name: 'Tahsilat' })).toBeVisible({ timeout: 15_000 });

    // Kasa oturumu açık olmadığı için Nakit pasif, EFT otomatik seçili.
    await expect(page.locator('.sp-method-chip[data-method="Cash"]')).toBeDisabled();
    await expect(page.locator('.sp-method-chip[data-method="Cash"]')).toContainText('kasa kapalı');

    await selectMethod(page, 'BankCard');
    await setAmount(page, '100');
    await page.getByRole('button', { name: 'Ödemeyi Ekle' }).click();

    // Dürüst durum: manuel mutabakat. Asla "Hesap Ödendi".
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toHaveCount(0);
    // Kilitliyken tahsilat formu hiç çizilmez.
    await expect(page.getByRole('button', { name: 'Ödemeyi Ekle' })).toHaveCount(0);

    // Sunucu gerçeği: hiçbir tahsis oluşmadı, kalan tutar aynı, çözülmemiş ödeme kayıtlı.
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(0);
    expect(summary.remainingAmount).toBe(100);
    expect(summary.allocations).toHaveLength(0);
    expect(summary.unsettledPayment, 'unsettledPayment must be reported by the server').toBeTruthy();

    // Sayfa yenilenince kilit, sayfanın kendi belleğinden değil sunucudan türetilir.
    await page.reload();
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: 'Ödemeyi Ekle' })).toHaveCount(0);

    // Aynı hesabı taze bir sekmede açan ikinci bir kasiyer ekranı da aynı kilidi görür.
    const freshTab = await context.newPage();
    await openSplitPayment(freshTab, billId);
    await expect(freshTab.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 10_000 });
    await expect(freshTab.getByRole('button', { name: 'Ödemeyi Ekle' })).toHaveCount(0);
    await freshTab.close();
  });

  test('sunucu, çözülmemiş kart tahsilatı varken doğrudan gelen yeni tahsilatı reddeder', async ({ page }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    const tendersUrl = `/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`;

    const first = await page.request.post(tendersUrl, {
      data: { Method: 'BankCard', Amount: 100, IdempotencyKey: crypto.randomUUID(), Note: null },
    });
    expect(first.ok()).toBeTruthy();
    expect((await first.json()).outcome).toBe('RequiresReconciliation');

    // Arayüz kilidini tamamen atlayan, betikle/tekrarlanan istek: sunucu yine reddetmeli.
    for (const method of ['BankCard', 'Eft']) {
      const second = await page.request.post(tendersUrl, {
        data: { Method: method, Amount: 100, IdempotencyKey: crypto.randomUUID(), Note: null },
      });
      expect(second.status(), `${method} retry must be rejected`).toBe(409);
      const body = await second.json();
      expect(body.error.code).toBe('TENDER_UNSETTLED_PAYMENT_EXISTS');
      // Kullanıcıya dönen metin Türkçe ve ham istisna adı içermez.
      expect(body.error.message).not.toMatch(/Exception|System\./);
    }

    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocations).toHaveLength(0);
    expect(summary.remainingAmount).toBe(100);
  });
});
