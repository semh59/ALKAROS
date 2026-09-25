// ALKAROS Waiter PWA — guest (QR) orders waiting for a waiter's accept/
// reject, and sending a table's check to the cashier (V1-WTR-049, step
// 13/? of docs/engineering/garson-refactor-plan.md's Section 2).

import { state, el, measureChrome } from '../state.js';
import { escapeHtml, formatMoney, formatClock } from '../util.js';
import { activeItems, loadOrder, renderBill, closeBill } from './bill.js';
import { showScreen, loadTables } from '../screens/tables.js';
import { afterDraftChange } from '../screens/menu.js';
import { openOptions, closeOptions } from '../options-sheet.js';
import { toast } from '../toast.js';
import { apiUrl, api } from '../api.js';

export async function loadPending() {
  const result = await api(apiUrl('/orders/pending'));
  state.pending = result.ok && Array.isArray(result.data) ? result.data : [];
  renderPendingBanner();
}

export function renderPendingBanner() {
  const count = state.pending.length;
  el.pendingBanner.hidden = count === 0;
  if (count > 0) {
    const first = state.pending[0];
    el.pendingTitle.textContent = count === 1
      ? `${first.tableNumber} masası sipariş verdi`
      : `${count} masa sipariş verdi`;
    el.pendingSub.textContent = count === 1
      ? `${first.itemCount} kalem • ${formatMoney(first.total)} • onayınızı bekliyor`
      : 'Onayınızı bekliyor';
  }
  measureChrome();
}

export function openPendingSheet() {
  if (state.pending.length === 0) return;
  const body = state.pending.map((order) => `
    <div class="line">
      <div class="line-main">
        <div class="line-top">
          <span class="line-name">${escapeHtml(order.tableNumber)} masası</span>
          <span class="line-total">${escapeHtml(formatMoney(order.total))}</span>
        </div>
        <div class="line-unit">${escapeHtml(order.itemCount)} kalem • ${escapeHtml(formatClock(order.createdAt))}</div>
      </div>
      <div class="line-side">
        <button type="button" class="btn btn-send btn-compact" data-accept="${escapeHtml(order.orderId)}">Onayla</button>
        <button type="button" class="btn-void" data-reject="${escapeHtml(order.orderId)}">Reddet</button>
      </div>
    </div>`).join('');
  openOptions('pending', 'Misafir siparişi', 'QR ile verilen siparişler onayınızı bekliyor', body, '', '', '');
  el.optionsConfirm.hidden = true;
}

// V1-RMD-175: disables both buttons on the same line for the duration of
// the call, re-enabling them only on failure - success already
// re-renders the sheet (or closes it), which throws these nodes away.
export async function resolvePendingGuarded(button, orderId, accept) {
  const line = button.closest('.line-side') || button.parentElement;
  const pair = line ? line.querySelectorAll('[data-accept], [data-reject]') : [button];
  pair.forEach((candidate) => { candidate.disabled = true; });
  try {
    await resolvePending(orderId, accept);
  } finally {
    pair.forEach((candidate) => { candidate.disabled = false; });
  }
}

// Accept and reject both need the order's current row version, which the
// summary does not carry - so it is read first rather than guessed.
async function resolvePending(orderId, accept) {
  const detail = await api(apiUrl(`/orders/${orderId}`));
  if (!detail.ok) { toast(detail.message, { warning: true }); return; }

  const body = accept
    ? { expectedRowVersion: detail.data.rowVersion, notes: null }
    : { expectedRowVersion: detail.data.rowVersion, reason: 'Garson reddetti' };
  const result = await api(apiUrl(`/orders/${orderId}/${accept ? 'accept' : 'reject'}`), { method: 'POST', body });

  if (!result.ok) { toast(result.message, { warning: true }); return; }
  toast(accept
    ? `${detail.data.tableNumber} siparişi mutfağa gönderildi.`
    : `${detail.data.tableNumber} siparişi reddedildi.`);

  await loadPending();
  await loadTables();
  if (state.pending.length === 0) closeOptions(); else openPendingSheet();
  if (state.table) { await loadOrder(state.table.id); renderBill(); }
}

// ══ Sending the check to the cashier ═══════════════════════════════
// V1-ORD-006. The party has eaten and is walking to the till; the table has
// to be free for the next one before they get there. The check keeps its own
// identity and waits at the cashier — it is not closed and nothing is paid
// here.

export function openSendToCashierSheet() {
  if (!state.table || !state.order) return;

  const total = activeItems().reduce((sum, item) => sum + item.totalPrice, 0);
  const body = `
    <div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>Hesap kasaya gider ve masa yeni müşteriye açılır. Bu adım ödeme almaz.</span>
    </div>
    <div class="line">
      <div class="line-main">
        <div class="line-top">
          <span class="line-name">${escapeHtml(state.table.number)} masası</span>
          <span class="line-total">${escapeHtml(formatMoney(total))}</span>
        </div>
        <div class="line-unit">${escapeHtml(activeItems().length)} kalem</div>
      </div>
    </div>`;

  state.optionsContext = { orderId: state.order.orderId, tableId: state.table.id };
  openOptions('cashier', 'Hesabı kasaya gönder', '', body, 'Kasaya gönder', '', '');
}

export async function confirmSendToCashier() {
  const context = state.optionsContext;
  if (!context) return;
  el.optionsConfirm.disabled = true;

  const result = await api(apiUrl(`/orders/${context.orderId}/send-to-cashier`), {
    method: 'POST',
    body: { tableId: context.tableId }
  });
  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  const tableNumber = state.table.number;
  const sentOrderId = context.orderId;
  const sentTableId = context.tableId;
  closeOptions();
  closeBill();
  state.table = null;
  state.order = null;
  state.draft = [];
  state.draftEpoch += 1;
  await loadTables();
  afterDraftChange();
  showScreen('tables');
  if (result.data.alreadySent) {
    toast(`${tableNumber} hesabı zaten kasaya gönderilmişti.`);
    return;
  }
  // A check sent by mistake goes back to its table with one tap while no money has moved (V1-RMD-281).
  toast(`${tableNumber} hesabı kasaya gönderildi, masa boşaldı.`, {
    undo: () => recallCheckFromCashier(sentOrderId, sentTableId, tableNumber)
  });
}

export async function recallCheckFromCashier(orderId, tableId, tableNumber) {
  const result = await api(apiUrl(`/orders/${orderId}/recall-from-cashier`), {
    method: 'POST',
    body: { tableId }
  });
  if (!result.ok) {
    toast(result.message, { warning: true });
    return;
  }
  await loadTables();
  toast(`${tableNumber} hesabı masaya geri alındı.`);
}
