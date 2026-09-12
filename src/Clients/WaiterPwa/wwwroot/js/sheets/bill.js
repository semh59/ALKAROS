// ALKAROS Waiter PWA — the bill: its computed totals, its rendered lines,
// and the sheet/column that shows them (V1-WTR-045, step 9/? of
// docs/engineering/garson-refactor-plan.md's Section 2, the plan's own
// third screen module in its 2.4 ordering).
//
// activeItems() moved here too, even though several OTHER not-yet-moved
// sheets (void, void-sent, comp, transfer) call it: it is genuinely "which
// of this order's items are still on the bill", the same question every
// one of bill.js's own exports answers, and none of those other callers
// exist as modules yet — they will import it from here when they do,
// exactly the one-directional dependency every other extraction in this
// refactor has kept (a not-yet-extracted part of waiter-app.js importing
// FROM an already-extracted module is fine; the reverse is not).

import { state, el, seatLabel } from '../state.js';
import { escapeHtml, formatMoney, formatQuantity, formatClock, courseLabel } from '../util.js';
import { featureEnabled } from '../features.js';
import { apiUrl, api } from '../api.js';

// The bill is always the server's answer, never a local accumulation.
// Exported: several not-yet-extracted sheets (void, void-sent, comp,
// transfer) reload the order the same way after their own mutation - the
// same one-directional dependency this file's activeItems() already
// documents above.
export async function loadOrder(tableId) {
  const result = await api(apiUrl(`/orders/table/${tableId}`));
  state.order = result.ok ? result.data : null;
  return result;
}

// V1-WTR-025: printed on the whole-plan kitchen ticket for prep
// visibility but not yet called in — distinct from NotSent (never
// reached the kitchen at all) and Sent (actively being prepared).
// Exported: the not-yet-extracted void-sent sheet (waiter-app.js) shows
// the same label in its own confirmation callout.
export const KITCHEN_STATE = {
  notsent: { label: 'Gönderilmedi', cls: '' },
  held: { label: 'Bekletiliyor', cls: 'is-held' },
  sent: { label: 'Mutfakta', cls: 'is-sent' },
  preparing: { label: 'Hazırlanıyor', cls: 'is-preparing' },
  ready: { label: 'Hazır', cls: 'is-ready' },
  served: { label: 'Servis edildi', cls: 'is-ready' },
  cancelled: { label: 'İptal', cls: '' }
};

export function activeItems() {
  if (!state.order || !state.order.items) return [];
  // A cancelled or wasted line stays in the DTO; it is history, not a bill
  // line the waiter can still act on.
  return state.order.items.filter((item) => item.status !== 'Cancelled' && item.status !== 'Waste');
}

// A modifier's own priceDelta is per portion; a half-portion order still
// charges a whole extra (a shot of syrup costs the same whether the drink
// itself is 0,5 or 1 - the server's own AdjustmentCalculator rounds the
// same way, this only has to match it for the on-screen estimate).
// Exported: the not-yet-extracted product sheet (waiter-app.js) needs the
// same count while a waiter is still choosing a quantity, before any line
// exists to render.
export function modifierCountFor(quantity) {
  return Math.max(1, Math.ceil(quantity));
}

function lineExtras(line) {
  const count = modifierCountFor(line.quantity);
  return line.modifiers.reduce((sum, modifier) => sum + modifier.priceDelta * count, 0);
}

export function draftTotal() {
  return state.draft.reduce((sum, line) => sum + line.price * line.quantity + lineExtras(line), 0);
}

// On a tablet the bill is a fixed column, so these calls are no-ops there -
// the sheet ignores its own transform under the wide media query.
export function openBill() {
  el.billSheet.classList.add('is-open');
  el.billBackdrop.classList.add('is-open');
}

export function closeBill() {
  el.billSheet.classList.remove('is-open');
  el.billBackdrop.classList.remove('is-open');
}

// "Turu tekrarla" is the last round the server itself recorded, grouped by
// the createdAt the DTO now carries (V1-RMD-146) - not a guess kept on this
// device.
export function lastRound() {
  const items = activeItems();
  if (items.length === 0) return null;
  const newest = items.reduce((latest, item) =>
    (!latest || item.createdAt > latest ? item.createdAt : latest), null);
  if (!newest) return null;
  const round = items.filter((item) => item.createdAt === newest);
  return round.length > 0 ? round : null;
}

export function renderQuickSend() {
  if (state.draft.length > 0) return '';
  const round = lastRound();
  if (!round) return '';
  const total = round.reduce((sum, item) => sum + item.totalPrice, 0);
  const names = round.map((item) => `${formatQuantity(item.quantity)}× ${item.productName}`).join(', ');
  return `
    <div class="group-label">Hızlı gönder</div>
    <div class="quick">
      <button type="button" class="quick-btn is-primary" data-quick-repeat="1">
        <span class="title">Turu tekrarla</span>
        <span class="sub">${escapeHtml(formatMoney(total))}</span>
      </button>
      <div class="quick-detail">${escapeHtml(names)}</div>
    </div>`;
}

export function renderBill() {
  if (!state.table) {
    el.billBody.innerHTML = '<div class="empty">Bir masaya dokunun.</div>';
    el.billTotal.textContent = formatMoney(0);
    el.billSub.textContent = '';
    el.btnAddItems.hidden = true;
    el.btnPartySize.hidden = true;
    el.btnMoveTable.hidden = true;
    el.btnSendToCashier.hidden = true;
    return;
  }
  el.btnAddItems.hidden = false;

  const sent = activeItems();
  // foundations §0.1: the money on screen is the server's own figure, not a
  // sum this client recomputes. Adding up the line prices ignored anything
  // applied at order level — a comped line or a bill discount — so the
  // table tile (which does use the server's number) and the bill footer
  // disagreed, and the waiter quoted the wrong one to the guest. Only the
  // unsent round, which the server has not seen yet, is estimated here.
  const sentTotal = state.order ? state.order.totalAmount : 0;
  el.billTotal.textContent = formatMoney(sentTotal + draftTotal());
  // V1-WTR-015: order.partySize is the source of truth once a real order
  // exists; draftPartySize is only ever shown before that (the button
  // itself is hidden then, but a stale label would still be wrong).
  const partySize = state.order ? state.order.partySize : state.draftPartySize;
  const partySuffix = partySize ? ` • ${partySize} kişi` : '';
  el.billSub.textContent = (state.order
    ? `${sent.length} kalem • ${formatClock(state.order.createdAt)}`
    : 'Açık sipariş yok') + partySuffix;
  el.menuTableSub.textContent = el.billSub.textContent;

  // V1-WTR-015: editable only before the table's first round is ever
  // sent — once a real Order exists, order.partySize is the source of
  // truth and this task deliberately does not offer a way to correct it
  // (see the task file's Out of scope).
  // V1-SET-004: also hidden outright when this deployment has party
  // size turned off.
  el.btnPartySize.hidden = !!state.order || !featureEnabled('partySize');
  el.btnPartySize.setAttribute('aria-label', state.draftPartySize ? `${state.draftPartySize} kişi` : 'Kişi sayısı');

  // V1-SET-004: hidden outright when this deployment has help requests
  // turned off — same treatment as the party-size button above.
  el.btnHelpRequest.hidden = !featureEnabled('helpRequest');

  // Transfer is offered only when the server's own AllowedCommands says so.
  el.btnMoveTable.hidden = state.table.allowedCommands.indexOf('Transfer') < 0;
  // V1-ORD-006: only an open check that has actually been ordered can go to
  // the till — there is nothing to send from an empty table, and an unsent
  // round would be left behind.
  el.btnSendToCashier.hidden = !state.order || sent.length === 0 || state.draft.length > 0;

  let html = renderQuickSend();

  if (state.draft.length > 0) {
    html += '<div class="group-label"><span>Yeni tur</span><span>gönderilmedi</span></div>';
    html += state.draft.map(renderDraftLine).join('');
  }

  // V1-RMD-166: found by the 2026-09-10 Garson audit — every item on
  // state.order used to render under the "Gönderildi" header just for
  // being non-cancelled, even one whose own kitchenState is NotSent (the
  // server persisted it, e.g. table-draft succeeded, but it was never
  // actually fired to the kitchen - the exact case V1-RMD-164 fixed the
  // correction path for). The section header said "Sent" while the
  // line's own badge said "Not sent", directly contradicting it. Split
  // into two groups instead of one.
  const awaitingDispatch = sent.filter((item) => (item.kitchenState || 'NotSent') === 'NotSent');
  const dispatched = sent.filter((item) => (item.kitchenState || 'NotSent') !== 'NotSent');

  if (awaitingDispatch.length > 0) {
    html += '<div class="group-label"><span>Gönderilmeyi bekliyor</span><span>gönderilmedi</span></div>';
    html += awaitingDispatch.map(renderSentLine).join('');
  }

  if (dispatched.length > 0) {
    // V1-RMD-167: found in independent review of V1-RMD-166 — sentTotal
    // is the server's total for the WHOLE order (every active item,
    // dispatched or not; §0.1 above is exactly why this never recomputes
    // it from lines). Printing it next to "Gönderildi" was correct only
    // when every active item is dispatched; the moment a table also has
    // an awaitingDispatch line, this label summed to more than the lines
    // shown under it — the same contradiction this split was meant to
    // remove, moved from the badge text into the money. No server figure
    // exists for "dispatched-only total", and one is never invented
    // client-side, so the label is shown only when it is truly this
    // group's total.
    const dispatchedLabel = awaitingDispatch.length === 0 ? escapeHtml(formatMoney(sentTotal)) : '';
    html += `<div class="group-label"><span>Gönderildi</span><span>${dispatchedLabel}</span></div>`;
    html += dispatched.map(renderSentLine).join('');
  }

  if (!html) html = '<div class="empty">Bu masada henüz sipariş yok.<br>Ürün ekleyerek başlayın.</div>';
  el.billBody.innerHTML = html;
}

export function renderDraftLine(line) {
  const extras = lineExtras(line);
  const count = modifierCountFor(line.quantity);
  // A count is shown only where it changes what is charged. A free
  // instruction ("az pişmiş") never reads as "2× az pişmiş".
  const chips = line.modifiers.map((modifier) => `
    <span class="chip-mod${modifier.priceDelta > 0 ? ' is-paid' : ''}">
      ${modifier.priceDelta > 0 && count > 1 ? `${escapeHtml(formatQuantity(count))}× ` : ''}${escapeHtml(modifier.name)}
    </span>`).join('');
  // V1-WTR-016: the server always resolves the real price from the
  // catalog at submit time (never trusts this cached value), so this is
  // purely "don't let the waiter be surprised" - a stale line still
  // sends correctly, this just lets them see and match the real price
  // first.
  const liveProduct = state.products.find((candidate) => candidate.id === line.productId);
  const priceChanged = liveProduct && liveProduct.price !== line.price;
  const priceNotice = priceChanged
    ? `<div class="line-price-changed">
         Fiyat güncellendi: ${escapeHtml(formatMoney(liveProduct.price))}
         <button type="button" data-update-price="${escapeHtml(line.id)}">Güncelle</button>
       </div>`
    : '';
  return `
    <div class="line">
      <div class="line-main">
        <div class="line-top">
          <span class="line-name">${escapeHtml(line.name)}</span>
          <span class="line-total">${escapeHtml(formatMoney(line.price * line.quantity + extras))}</span>
        </div>
        <div class="line-unit">${escapeHtml(formatQuantity(line.quantity))} × ${escapeHtml(formatMoney(line.price))}${
          line.seatId && seatLabel(line.seatId) ? ` • ${escapeHtml(seatLabel(line.seatId))}` : ''}${
          line.courseNumber ? ` • ${escapeHtml(courseLabel(line.courseNumber))}` : ''}</div>
        ${chips ? `<div class="line-chips">${chips}</div>` : ''}
        ${priceNotice}
        <input class="note" type="text" maxlength="200" data-note="${escapeHtml(line.id)}"
               value="${escapeHtml(line.note)}" placeholder="Not (az pişmiş, acısız…)"
               aria-label="${escapeHtml(line.name)} için not">
      </div>
      <div class="line-side">
        <div class="stepper">
          <button type="button" data-step="-" data-line="${escapeHtml(line.id)}" aria-label="Azalt">−</button>
          <span class="qty">${escapeHtml(formatQuantity(line.quantity))}</span>
          <button type="button" data-step="+" data-line="${escapeHtml(line.id)}" aria-label="Artır">+</button>
        </div>
      </div>
    </div>`;
}

export function renderSentLine(item) {
  const kitchen = KITCHEN_STATE[(item.kitchenState || '').toLowerCase()] || null;
  const chips = (item.modifiers || []).map((modifier) => `
    <span class="chip-mod${modifier.priceDelta > 0 ? ' is-paid' : ''}">
      ${modifier.quantity > 1 ? `${escapeHtml(formatQuantity(modifier.quantity))}× ` : ''}${escapeHtml(modifier.name)}
    </span>`).join('');

  // V1-RMD-143: how many more of this product the mapped stock could still
  // cover. Null means the product is not stock-tracked at all - which is not
  // the same as none left, so nothing is shown.
  const stock = item.availableStockQuantity !== null && item.availableStockQuantity !== undefined
    ? `<span class="product-stock${item.availableStockQuantity <= 0 ? ' is-out' : (item.availableStockQuantity < 5 ? ' is-low' : '')}">
         Kalan ${escapeHtml(formatQuantity(item.availableStockQuantity))}
       </span>`
    : '';

  // V1-RMD-154/155: which of the two void paths this line belongs to.
  //
  // A line that has not gone to the kitchen voids for free. One that has
  // needs the grant-gated path, which cancels the kitchen ticket and gives
  // the stock back — and for a waiter raises a manager approval. Offering
  // no button at all (which is what V1-RMD-154 left, because that endpoint
  // had no client) meant a wrongly-sent dish could not be cancelled at all.
  //
  // V1-RMD-168: found by the 2026-09-10 Garson audit (foundations.md
  // §0.2, "which action is valid is the server's answer") — this used to
  // derive both flags itself from kitchenState alone, missing the Status
  // half of each real eligibility check
  // (ItemExceptionHandler.VoidItemAsync, SentItemVoidStore.VoidAsync).
  // canVoid/canVoidSent are now the server's own computed fields; the
  // client only shows or hides a button, never re-derives whether the
  // click would succeed.
  const canVoid = item.canVoid;
  const canVoidSent = item.canVoidSent;
  // V1-RMD-177: found by the 2026-09-10 Garson audit — /comp had no
  // client at all. canComp is the server's own field (mirrors
  // ItemExceptionHandler.ApplyComplimentaryAsync's eligibility check),
  // same pattern as canVoid/canVoidSent above.
  const canComp = item.canComp;

  return `
    <div class="line">
      <div class="line-main">
        <div class="line-top">
          <span class="line-name">${escapeHtml(item.productName)}</span>
          <span class="line-total">${escapeHtml(formatMoney(item.totalPrice))}</span>
        </div>
        <div class="line-unit">${escapeHtml(formatQuantity(item.quantity))} × ${escapeHtml(formatMoney(item.unitPrice))}${
          item.createdAt ? ` • ${escapeHtml(formatClock(item.createdAt))}` : ''}${
          item.seatId && seatLabel(item.seatId) ? ` • ${escapeHtml(seatLabel(item.seatId))}` : ''}${
          item.courseNumber ? ` • ${escapeHtml(courseLabel(item.courseNumber))}` : ''}</div>
        ${chips ? `<div class="line-chips">${chips}</div>` : ''}
        <div class="line-chips">
          ${kitchen ? `<span class="kitchen-state ${kitchen.cls}"><span class="dot"></span>${escapeHtml(kitchen.label)}</span>` : ''}
          ${stock}
        </div>
        ${item.specialInstructions ? `<div class="line-unit">${escapeHtml(item.specialInstructions)}</div>` : ''}
      </div>
      <div class="line-side">
        ${canComp ? `<button type="button" class="btn btn-quiet btn-compact" data-comp="${escapeHtml(item.itemId)}">İkram</button>` : ''}
        ${canVoid ? `<button type="button" class="btn-void" data-void="${escapeHtml(item.itemId)}">İptal</button>` : ''}
        ${canVoidSent ? `<button type="button" class="btn-void" data-void-sent="${escapeHtml(item.itemId)}">İptal iste</button>` : ''}
        ${(item.kitchenState || '').toLowerCase() === 'held' && item.courseNumber
          ? `<button type="button" class="btn btn-quiet btn-compact" data-fire-course="${item.courseNumber}">Kursu ateşle</button>`
          : ''}
      </div>
    </div>`;
}
