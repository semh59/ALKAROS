// ALKAROS Waiter PWA — the "bekleyen ve hatalı siparişler" sheet
// (V1-WTR-050, step 14/? of docs/engineering/garson-refactor-plan.md's
// Section 2).
//
// dismissFailedOrder()/clearAllFailedOrders() (the mutating half) stay in
// waiter-app.js, not here: both call persistQueue(), which is
// offline-queue.js's own concern and does not exist as a module yet - the
// same open/confirm split V1-WTR-044 (party-size.js) already established
// for exactly this kind of not-yet-ready dependency.

import { state, el } from '../state.js';
import { escapeHtml, formatQuantity } from '../util.js';
import { openOptions } from '../options-sheet.js';

// V1-RMD-171: found by the 2026-09-10 Garson audit — the ribbon's own
// "N hatalı" count was the whole story: no table, no content, and no
// way to clear it. This opens a real, reachable list of both the still-
// retrying (offline queue) and the permanently rejected (failed) rounds,
// with what each one actually contained and a way to dismiss a
// permanently-failed one once the waiter has dealt with it by hand
// (re-entering it, or telling the guest).
export function openFailedOrdersSheet() {
  const queued = state.offlineQueue.map((payload) => queuedOrderRow(payload, false));
  const failed = state.failedOrders.map((payload) => queuedOrderRow(payload, true));
  const rows = queued.concat(failed);

  const body = rows.length === 0
    ? '<div class="empty">Bekleyen veya hatalı sipariş yok.</div>'
    : `<div class="opts">${rows.join('')}</div>`;

  openOptions(
    'failed-orders',
    'Bekleyen ve hatalı siparişler',
    queued.length > 0
      ? `${queued.length} sunucuya ulaşmayı bekliyor, gönderilmemiş değil.`
      : '',
    body,
    failed.length > 0 ? 'Hatalı olanların tümünü temizle' : '',
    '', '');
  el.optionsConfirm.className = 'btn btn-danger';
  el.optionsConfirm.hidden = failed.length === 0;
}

function queuedOrderRow(payload, isFailed) {
  const itemCount = (payload.items || []).reduce((sum, item) => sum + (item.quantity || 0), 0);
  const itemNames = (payload.items || []).map((item) => item.name || item.productName).join(', ');
  return `
    <div class="opt" style="cursor:default">
      <span class="opt-box is-round">
        <svg class="icon" aria-hidden="true"><use href="#ico-${isFailed ? 'alert' : 'bell'}"/></svg>
      </span>
      <span class="opt-name">
        ${escapeHtml(payload.tableNumber || '?')} masası — ${escapeHtml(formatQuantity(itemCount))} kalem
        <span class="line-unit">${escapeHtml(itemNames)}</span>
        ${isFailed && payload.error ? `<span class="line-unit">${escapeHtml(payload.error)}</span>` : ''}
      </span>
      ${isFailed
        ? `<button type="button" class="btn-void" data-dismiss-failed="${escapeHtml(payload.id)}">Sil</button>`
        : ''}
    </div>`;
}
