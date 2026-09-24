import { test, expect } from '@playwright/test';
import { readSeed, loginCashier } from '../lib/testHelpers.js';
import { createBill, openSplitPayment, readTenderSummary } from '../lib/paymentHelpers.js';

// V13-TBL-001 / V1-RMD-258: the flagship cross-client scenario. A card
// payment that could not be confirmed (the honest BankCard placeholder always
// answers "needs manual reconciliation") leaves an unresolved Payment on the
// bill. A manager on the PosTerminal floor screen must then be unable to move
// that table - a real guarantee proven from the actual screen the manager uses,
// not only from the isolated backend test - while a bill that is merely
// PARTLY paid (every payment settled) moves fine, its allocation staying on the
// same bill.
async function sessionTerminalId(page) {
  return (await (await page.request.get('/api/v1/auth/session/current')).json()).terminalId;
}

async function tableRow(page, terminalId, tableNumber) {
  const body = await (await page.request.get(`/api/v1/terminals/${terminalId}/table-management/tables`)).json();
  const rows = Array.isArray(body) ? body : body.tables || body.items || [];
  const row = rows.find((table) => table.tableNumber === tableNumber);
  expect(row, `table ${tableNumber} not in list`).toBeTruthy();
  return row;
}

async function pickTarget(page, targetNumber) {
  const select = page.getByLabel('Hedef masa');
  const value = await select.locator('option').evaluateAll(
    (options, number) => (options.find((option) => option.textContent.startsWith(`${number} `)) || {}).value,
    targetNumber,
  );
  expect(value, `target ${targetNumber} not offered`).toBeTruthy();
  await select.selectOption(value);
}

test.describe('Masa devri + ödeme kilidi, iki istemci (V13-TBL-001, V1-RMD-258)', () => {
  test('çözülmemiş kart tahsilatı olan masa PosTerminal\'den devredilemez; hiçbir şey değişmez', async ({ page, context }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = await sessionTerminalId(page);
    const [source, target] = seed.transferTables;
    const billId = await createBill(page, terminalId, seed, { quantity: 1, table: source });

    // Kasiyer PWA'sı: kart tahsilatı denenir, dürüstçe "manuel mutabakat" olur.
    const tender = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'BankCard', Amount: 100, IdempotencyKey: crypto.randomUUID(), Note: null },
    });
    expect((await tender.json()).outcome).toBe('RequiresReconciliation');
    const cashierTab = await context.newPage();
    await openSplitPayment(cashierTab, billId);
    await expect(cashierTab.getByText('Manuel mutabakat gerekiyor')).toBeVisible({ timeout: 15_000 });

    const before = await tableRow(page, terminalId, source.tableNumber);
    const targetBefore = await tableRow(page, terminalId, target.tableNumber);
    expect(before.status).toBe('Occupied');

    // PosTerminal: müdür aynı masayı başka masaya taşımaya çalışır.
    await page.goto('/tables');
    await page.getByRole('button', { name: new RegExp(`^${source.tableNumber} masasını seç`) }).click();
    await page.getByRole('button', { name: 'Masa değiştir', exact: true }).click();
    await pickTarget(page, target.tableNumber);
    await page.getByLabel('Açıklama').fill('E2E masa değişikliği');
    await page.getByRole('button', { name: 'Onayla', exact: true }).click();

    // Başarı bildirimi ASLA görünmez; bir hata/çakışma uyarısı görünür.
    const alerts = page.getByRole('alert');
    await expect(alerts.first()).toBeVisible({ timeout: 10_000 });
    const shown = (await alerts.allInnerTexts()).join(' | ').replace(/\s+/g, ' ');
    // Müdür NEDENİ görmeli: çözülmemiş ödeme. "Tekrar deneyin" yanıltıcıdır
    // (ödeme çözülene kadar tekrar denemek asla işe yaramaz) ve "masa güncellendi"
    // yanlış bir çakışma iddiasıdır; ikisi de görünmemeli.
    expect(shown).toContain('çözülmemiş bir ödeme');
    expect(shown).not.toContain('Tekrar deneyin');
    expect(shown).not.toContain('Masa güncellendi');
    expect(shown).not.toContain('tamamlandı');

    // Sunucu gerçeği: iki masa da, hesap da olduğu gibi.
    const after = await tableRow(page, terminalId, source.tableNumber);
    const targetAfter = await tableRow(page, terminalId, target.tableNumber);
    expect(after.status).toBe('Occupied');
    expect(after.rowVersion).toBe(before.rowVersion);
    expect(after.currentBillId).toBe(before.currentBillId);
    expect(targetAfter.status).toBe(targetBefore.status);
    expect(targetAfter.rowVersion).toBe(targetBefore.rowVersion);
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocations).toHaveLength(0);
    expect(summary.unsettledPayment).toBeTruthy();
  });

  test('kısmen ödenmiş (tüm ödemeleri çözülmüş) masa devredilir; tahsis aynı hesapta kalır', async ({ page }) => {
    const seed = readSeed();
    await loginCashier(page, seed);
    const terminalId = await sessionTerminalId(page);
    const [source, target] = seed.transferTables.slice(2, 4);
    const billId = await createBill(page, terminalId, seed, { quantity: 1, table: source });

    const partial = await page.request.post(`/api/v1/terminals/${terminalId}/billing/bills/${billId}/tenders/`, {
      data: { Method: 'Eft', Amount: 40, IdempotencyKey: crypto.randomUUID(), Note: null },
    });
    expect(partial.ok()).toBeTruthy();

    await page.goto('/tables');
    await page.getByRole('button', { name: new RegExp(`^${source.tableNumber} masasını seç`) }).click();
    await page.getByRole('button', { name: 'Masa değiştir', exact: true }).click();
    await pickTarget(page, target.tableNumber);
    await page.getByLabel('Açıklama').fill('E2E masa değişikliği');
    await page.getByRole('button', { name: 'Onayla', exact: true }).click();
    await expect(page.getByText(/tamamlandı/)).toBeVisible({ timeout: 10_000 });

    const sourceAfter = await tableRow(page, terminalId, source.tableNumber);
    const targetAfter = await tableRow(page, terminalId, target.tableNumber);
    expect(sourceAfter.status).toBe('Available');
    expect(targetAfter.status).toBe('Occupied');
    expect(targetAfter.currentBillId).toBe(billId);

    // Para ne yaratıldı ne yok oldu: aynı hesap, aynı 40 TL tahsis, 60 TL kalan.
    const summary = await readTenderSummary(page, terminalId, billId);
    expect(summary.allocatedTotal).toBe(40);
    expect(summary.remainingAmount).toBe(60);
    expect(summary.allocations).toHaveLength(1);
  });
});
