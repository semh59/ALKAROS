// ALKAROS Waiter PWA — void (free and grant-gated) and complimentary
// item sheets (V1-WTR-047, step 11/? of
// docs/engineering/garson-refactor-plan.md's Section 2).

import { state, el } from '../state.js';
import { escapeHtml, formatMoney, formatQuantity, randomUUID } from '../util.js';
import { activeItems, KITCHEN_STATE, renderBill, loadOrder } from './bill.js';
import { loadTables } from '../screens/tables.js';
import { openOptions, closeOptions } from '../options-sheet.js';
import { toast } from '../toast.js';
import { apiUrl, api } from '../api.js';

// VoidReasonCatalog (src/Modules/Orders/ItemExceptions/ReasonCatalogs.cs).
// The codes are the server's; only the wording is ours.
const VOID_REASONS = [
  { code: 'CustomerChange', label: 'Müşteri vazgeçti' },
  { code: 'OperatorError', label: 'Yanlış girdim' },
  { code: 'ProductUnavailable', label: 'Ürün kalmadı' },
  { code: 'DuplicateEntry', label: 'İki kez girilmiş' }
];

// V1-RMD-177: ComplimentaryReasonCatalog
// (src/Modules/Orders/ItemExceptions/ReasonCatalogs.cs). The codes are
// the server's; only the wording is ours.
const COMP_REASONS = [
  { code: 'ServiceApology', label: 'Hizmet için özür' },
  { code: 'CustomerSatisfaction', label: 'Müşteri memnuniyeti' },
  { code: 'VIPGuest', label: 'VIP misafir' },
  { code: 'ManagerPromotion', label: 'Yönetici promosyonu' }
];

export function openVoidSheet(itemId) {
  const item = activeItems().find((candidate) => candidate.itemId === itemId);
  if (!item || !state.order) return;

  state.optionsContext = { itemId, reason: null };
  const body = `
    <div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>İptal geri alınamaz ve kaydı tutulur. Bir gerekçe seçin.</span>
    </div>
    <div class="opts">
      ${VOID_REASONS.map((reason) => `
        <button type="button" class="opt" data-reason="${escapeHtml(reason.code)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(reason.label)}</span>
        </button>`).join('')}
    </div>`;
  openOptions('void', 'Kalemi iptal et', `${formatQuantity(item.quantity)} × ${item.productName}`,
    body, 'İptal et', '', '');
  el.optionsConfirm.className = 'btn btn-danger';
  el.optionsConfirm.disabled = true;
}

export async function confirmVoid() {
  const context = state.optionsContext;
  if (!context || !context.reason || !state.order) return;
  el.optionsConfirm.disabled = true;
  const result = await api(
    apiUrl(`/orders/${state.order.orderId}/items/${context.itemId}/void`),
    { method: 'POST', body: { expectedRowVersion: state.order.rowVersion, reasonCode: context.reason } });

  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }
  closeOptions();
  await loadOrder(state.table.id);
  await loadTables();
  renderBill();
  toast('Kalem iptal edildi.');
}

// Keyed by item id so a retry after approval reuses the same request.
const pendingVoidKeys = new Map();

// V1-RMD-155: the grant-gated path for a line the kitchen already has.
// Unlike the free void it may not resolve immediately — a waiter's request
// goes to a manager and comes back 202 Pending. The idempotency key is
// generated once per attempt and kept, so retrying after the manager
// approves resolves to that same request instead of opening a second one.
export function openVoidSentSheet(itemId) {
  const item = activeItems().find((candidate) => candidate.itemId === itemId);
  if (!item || !state.order) return;

  state.optionsContext = {
    itemId,
    reason: null,
    idempotencyKey: pendingVoidKeys.get(itemId) || randomUUID()
  };

  const kitchen = KITCHEN_STATE[(item.kitchenState || '').toLowerCase()];
  const body = `
    <div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>Bu ürün mutfağa gitti${kitchen ? ` (${escapeHtml(kitchen.label)})` : ''}.
      İptali yönetici onayına gidebilir. Onaylanırsa mutfak bileti de iptal edilir
      ve stok geri alınır.</span>
    </div>
    <div class="opts">
      ${VOID_REASONS.map((reason) => `
        <button type="button" class="opt" data-reason="${escapeHtml(reason.code)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(reason.label)}</span>
        </button>`).join('')}
    </div>`;

  openOptions('void-sent', 'Mutfaktaki ürünü iptal et',
    `${formatQuantity(item.quantity)} × ${item.productName}`, body, 'İptal iste', '', '');
  el.optionsConfirm.className = 'btn btn-danger';
  el.optionsConfirm.disabled = true;
}

export async function confirmVoidSent() {
  const context = state.optionsContext;
  if (!context || !context.reason || !state.order) return;
  el.optionsConfirm.disabled = true;

  const result = await api(
    apiUrl(`/orders/${state.order.orderId}/items/${context.itemId}/void-sent`),
    {
      method: 'POST',
      body: {
        idempotencyKey: context.idempotencyKey,
        expectedRowVersion: state.order.rowVersion,
        reasonCode: context.reason
      }
    });

  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  closeOptions();
  if (result.data && result.data.status === 'Pending') {
    // Keep the key: the same request has to be resent once a manager
    // resolves it, or a second grant would be raised for one decision.
    pendingVoidKeys.set(context.itemId, context.idempotencyKey);
    toast('İptal yönetici onayına gönderildi.', { warning: true });
    return;
  }

  pendingVoidKeys.delete(context.itemId);
  await loadOrder(state.table.id);
  await loadTables();
  renderBill();
  const restored = result.data && result.data.stockRestored;
  toast(restored ? 'Ürün iptal edildi, stok geri alındı.' : 'Ürün iptal edildi.');
}

// V1-RMD-177: found by the 2026-09-10 Garson audit — /comp had no client
// anywhere. bills.comp is grant-class the same way bills.void is on the
// void-sent path above: a role that holds it outright (cashier/supervisor
// /manager) applies directly, a waiter's request goes to a manager and may
// come back 202 Pending — same idempotency-key-survives-a-retry pattern.

const pendingCompKeys = new Map();

export function openCompSheet(itemId) {
  const item = activeItems().find((candidate) => candidate.itemId === itemId);
  if (!item || !state.order) return;

  state.optionsContext = {
    itemId,
    reason: null,
    idempotencyKey: pendingCompKeys.get(itemId) || randomUUID()
  };

  const body = `
    <div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>Ürün ücretsiz sayılır, kalıcı olarak kayda geçer. Bir gerekçe seçin.</span>
    </div>
    <div class="opts">
      ${COMP_REASONS.map((reason) => `
        <button type="button" class="opt" data-reason="${escapeHtml(reason.code)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(reason.label)}</span>
        </button>`).join('')}
    </div>`;

  openOptions('comp', 'Ürünü ikram et',
    `${formatQuantity(item.quantity)} × ${item.productName}`, body, 'İkram et', '', '');
  el.optionsConfirm.className = 'btn btn-primary';
  el.optionsConfirm.disabled = true;
}

export async function confirmComp() {
  const context = state.optionsContext;
  if (!context || !context.reason || !state.order) return;
  el.optionsConfirm.disabled = true;

  const result = await api(
    apiUrl(`/orders/${state.order.orderId}/items/${context.itemId}/comp`),
    {
      method: 'POST',
      body: {
        idempotencyKey: context.idempotencyKey,
        expectedRowVersion: state.order.rowVersion,
        reasonCode: context.reason
      }
    });

  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  closeOptions();
  if (result.data && result.data.status === 'Pending') {
    pendingCompKeys.set(context.itemId, context.idempotencyKey);
    toast('İkram yönetici onayına gönderildi.', { warning: true });
    return;
  }

  pendingCompKeys.delete(context.itemId);
  await loadOrder(state.table.id);
  await loadTables();
  renderBill();
  // V1-WTR-012: personalBudgetRemaining is only set by the server when
  // this specific comp was resolved by the waiter's own per-day allowance
  // (not an outright bills.comp, not a delegation) — telling the waiter
  // what's left keeps the allowance usable without a separate screen.
  const remaining = result.data && result.data.personalBudgetRemaining;
  toast(remaining !== null && remaining !== undefined
    ? `Ürün ikram edildi. Bugünkü ikram hakkınızdan ${formatMoney(remaining)} kaldı.`
    : 'Ürün ikram edildi.');
}
