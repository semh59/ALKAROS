// ALKAROS Waiter PWA — the menu screen and the unsent round it composes
// (V1-WTR-046, step 10/? of docs/engineering/garson-refactor-plan.md's
// Section 2, paired with sheets/product-sheet.js per the plan's own note
// that the two are "birbirine en sıkı bağımlı" — tightly coupled).

import { state, el, persistDraftsByTable } from '../state.js';
import { escapeHtml, formatMoney, formatQuantity, randomUUID, renderStockBadge } from '../util.js';
import { draftTotal, renderBill } from '../sheets/bill.js';

export function renderCategories() {
  const all = [{ id: 'all', name: 'Tümü' }].concat(state.categories);
  el.categoryChips.innerHTML = all.map((category) => `
    <button type="button" class="chip" data-category="${escapeHtml(category.id)}"
            aria-pressed="${state.activeCategory === category.id}">${escapeHtml(category.name)}</button>`).join('');
}

export function draftQuantityOf(productId) {
  return state.draft
    .filter((line) => line.productId === productId)
    .reduce((sum, line) => sum + line.quantity, 0);
}

export function renderProducts() {
  const query = state.search.trim().toLocaleLowerCase('tr-TR');
  const visible = state.products.filter((product) => {
    const matchesCategory = state.activeCategory === 'all' || product.categoryCode === state.activeCategory;
    const matchesQuery = !query || product.name.toLocaleLowerCase('tr-TR').includes(query);
    return matchesCategory && matchesQuery;
  });

  if (visible.length === 0) {
    el.productList.innerHTML = '<div class="empty">Aramanıza uyan ürün yok.</div>';
    return;
  }

  let html = '';
  let lastCategory = null;
  const grouped = state.activeCategory === 'all' && !query;
  for (const product of visible) {
    if (grouped && product.categoryCode !== lastCategory) {
      lastCategory = product.categoryCode;
      html += `<div class="cat-label">${escapeHtml(product.categoryName || product.categoryCode)}</div>`;
    }
    const inDraft = draftQuantityOf(product.id);
    const hasOptions = product.modifierGroups.length > 0;
    // V1-WTR-055/056: same badge bill.js's renderSentLine already shows for
    // a sent line (V1-RMD-143), via the shared renderStockBadge — reused
    // here so the waiter sees it while still CHOOSING, not only after
    // adding. A product V1-WTR-054's catalog query would report at 0 or
    // less never reaches this list at all, so allowOut stays false.
    const remaining = renderStockBadge(product.remainingCount);
    const meta = hasOptions || remaining
      ? `<span class="product-meta">${hasOptions ? '<span class="product-options">Seçenekli</span>' : ''}${remaining}</span>`
      : '';
    html += `
      <div class="product-row">
        <button type="button" class="product" data-product="${escapeHtml(product.id)}">
          <span class="product-main">
            <span class="product-name">${escapeHtml(product.name)}</span>
            ${meta}
          </span>
          <span class="product-price">${formatMoney(product.price)}</span>
          ${inDraft > 0 ? `<span class="product-count">${escapeHtml(formatQuantity(inDraft))}</span>` : ''}
        </button>
        <button type="button" class="product-half" data-half="${escapeHtml(product.id)}"
                aria-label="${escapeHtml(product.name)} — miktar seç">½</button>
      </div>`;
  }
  el.productList.innerHTML = html;
}

// ══ The draft round ════════════════════════════════════════════════

export function addToDraft(product, quantity, modifiers, note, seatId, courseNumber) {
  // Lines that are identical in every respect merge; anything with its own
  // options, note, seat or course stays its own line so the kitchen ticket
  // reads right and the split screen can tell the seats/courses apart.
  const plain = (!modifiers || modifiers.length === 0) && !note;
  if (plain) {
    const existing = state.draft.find((line) =>
      line.productId === product.id && line.modifiers.length === 0 && !line.note
      && (line.seatId || null) === (seatId || null)
      && (line.courseNumber || null) === (courseNumber || null));
    if (existing) {
      existing.quantity = Math.round((existing.quantity + quantity) * 1000) / 1000;
      afterDraftChange();
      return;
    }
  }
  state.draft.push({
    id: randomUUID(),
    productId: product.id,
    name: product.name,
    price: product.price,
    quantity,
    modifiers: modifiers || [],
    note: note || '',
    seatId: seatId || null,
    courseNumber: courseNumber || null
  });
  afterDraftChange();
}

export function afterDraftChange() {
  const count = state.draft.reduce((sum, line) => sum + line.quantity, 0);
  el.cartCount.textContent = formatQuantity(count);
  el.cartTotal.textContent = formatMoney(draftTotal());
  const empty = state.draft.length === 0;
  el.btnSendFromMenu.disabled = empty;
  el.btnSendFromBill.disabled = empty;
  renderProducts();
  renderBill();

  // V1-RMD-170: keeps the CURRENT table's own unsent round in the same
  // held-drafts map (and so in localStorage) it lands in the moment the
  // waiter steps away — every keystroke while composing is already
  // durable, not only the state as of the last table switch.
  if (state.table) {
    if (state.draft.length > 0) {
      state.draftsByTable.set(state.table.id, { number: state.table.number, lines: state.draft });
    } else {
      state.draftsByTable.delete(state.table.id);
    }
    persistDraftsByTable();
  }
}
