import { randomUUID } from 'node:crypto';
import { test, expect } from '@playwright/test';
import { readSeed } from '../lib/testHelpers.js';
import { loginViaApi, createBill, openSplitPayment } from '../lib/paymentHelpers.js';

// V1-RMD-378 (module-by-module UI audit round 2, T7 - rol-arası haberleşme):
// a card payment "onay bekliyor" (V1-RMD-283's own two-person rule) needs a
// SECOND, different manager to approve it - but nothing here ever refreshed
// while the FIRST manager's screen sat open waiting for that decision. This
// proves it with two REAL, independent browser contexts (two different
// managers, two different terminals/sessions) - the first manager's page is
// never reloaded; only the second manager's own action (in a separate
// context) resolves it.
test.describe('Hesap Ödeme - onay bekleyen kart tahsilatı otomatik güncellenir (V1-RMD-378)', () => {
  test('ikinci müdürün onayı, ilk müdürün açık ekranına sayfa yenilenmeden yansır', async ({ page, browser }) => {
    const seed = readSeed();
    const terminalId = await loginViaApi(page, seed);
    const billId = await createBill(page, terminalId, seed, { quantity: 1 });
    const tender = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'BankCard', Amount: 100, IdempotencyKey: randomUUID(), Note: null },
    });
    expect((await tender.json()).outcome).toBe('RequiresReconciliation');

    // Birinci müdür: fiş numarasıyla bildirir, sonra bu SEKME AÇIK KALIR.
    await openSplitPayment(page, billId);
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 15_000 });
    await page.getByPlaceholder('Fiş numarası').fill('E2E-POLL-9001');
    await page.getByRole('button', { name: 'Kart çekildi: onaya gönder' }).click();
    await expect(page.getByText('Onay bekliyor: fiş E2E-POLL-9001')).toBeVisible();

    // İkinci müdür, TAMAMEN AYRI bir tarayıcı bağlamında (farklı çerez/oturum) onaylar - ilk
    // müdürün sekmesine hiçbir şekilde dokunulmaz, hiç yenilenmez.
    const secondContext = await browser.newContext();
    const secondPage = await secondContext.newPage();
    try {
      const secondTerminalId = await loginViaApi(secondPage, seed, seed.secondManagerUsername);
      const summaryResponse = await secondPage.request.get(`/api/v1/terminals/${secondTerminalId}/billing/bills/${billId}/tenders/`);
      const summary = await summaryResponse.json();
      const confirmationId = summary.unsettledPayment.pendingConfirmation.confirmationId;
      const approveResponse = await secondPage.request.post(
        `/api/v1/terminals/${secondTerminalId}/billing/bills/${billId}/tenders/confirmations/${confirmationId}/approve`,
        { data: { note: null } },
      );
      expect(approveResponse.ok()).toBeTruthy();
    } finally {
      await secondContext.close();
    }

    // Birinci müdürün sekmesi - reload() hiç çağrılmadı - kendiliğinden güncellenmeli. Tahsilat
    // hesabın tamamını karşıladığı için ekran doğrudan "Hesap Ödendi"ye geçer.
    await expect(page.getByText('Manuel mutabakat gerekiyor')).toBeHidden({ timeout: 8_000 });
    await expect(page.getByRole('heading', { name: 'Hesap Ödendi' })).toBeVisible();
  });
});
