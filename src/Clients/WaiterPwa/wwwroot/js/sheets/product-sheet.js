// ALKAROS Waiter PWA — the product sheet (quantity/seat/course/modifier
// picker) opened from the menu screen (V1-WTR-046, step 10/? of
// docs/engineering/garson-refactor-plan.md's Section 2, paired with
// screens/menu.js per the plan's own note that the two are tightly
// coupled).
//
// The two shared click/confirm routers (onOptionsBodyClick,
// onOptionsConfirm) stay in waiter-app.js — they dispatch across every
// sheet the app has, most of which are not their own module yet. Their
// "product"-mode branches call toggleModifier/updateProductSheetTotal/
// addToDraft, imported here like anything else waiter-app.js still needs
// from an already-extracted module.

import { state, el } from '../state.js';
import { escapeHtml, formatMoney, formatQuantity } from '../util.js';
import { featureEnabled } from '../features.js';
import { modifierCountFor } from './bill.js';
import { openOptions } from '../options-sheet.js';
import { toast } from '../toast.js';

export function openProductSheet(product, presetQuantity) {
  state.optionsContext = {
    product,
    quantity: presetQuantity || 1,
    chosen: new Map(),
    seatId: null,
    courseNumber: null
  };

  openOptions('product', product.name, '', productSheetHtml(), 'Adisyona ekle', 'Tutar', '');
  updateProductSheetTotal();
}

export function productSheetHtml() {
  const context = state.optionsContext;
  const quantities = [0.5, 1, 1.5, 2, 3];
  let html = `
    <div class="optgroup">
      <div class="optgroup-head"><span class="optgroup-name">Miktar</span>
        <span class="optgroup-rule">yarım porsiyon 0,5</span></div>
      <div class="qty-row">
        ${quantities.map((quantity) => `
          <button type="button" class="qty-quick" data-qty="${quantity}"
                  aria-pressed="${context.quantity === quantity}">${escapeHtml(formatQuantity(quantity))}</button>`).join('')}
      </div>
    </div>`;

  // V1-WTR-022: only offered when the table actually has a floor-plan
  // seat layout - a table with none simply never shows this optgroup.
  // V1-SET-004: AND only when this deployment has seat assignment on.
  if (state.tableSeats.length > 0 && featureEnabled('seatAssignment')) {
    html += `
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Koltuk</span>
          <span class="optgroup-rule">opsiyonel</span></div>
        <div class="qty-row">
          <button type="button" class="qty-quick" data-seat=""
                  aria-pressed="${!context.seatId}">Koltuksuz</button>
          ${state.tableSeats.map((seat) => `
            <button type="button" class="qty-quick" data-seat="${escapeHtml(seat.id)}"
                    aria-pressed="${context.seatId === seat.id}">${escapeHtml(seat.label)}</button>`).join('')}
        </div>
      </div>`;
  }

  // V1-WTR-025: optional — most orders have no course structure at all.
  // Choosing a course number here just tags the line; whether it goes
  // straight to the kitchen or is held with the rest of its course is
  // decided server-side when the whole draft is fired (Order.FireRound).
  // V1-SET-004: this whole optgroup only when this deployment has
  // course management on — the server would silently drop the field
  // anyway (TableDraftService), but showing a control that quietly does
  // nothing is worse than not showing it at all.
  if (featureEnabled('courseManagement')) {
    html += `
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Kurs</span>
          <span class="optgroup-rule">opsiyonel</span></div>
        <div class="qty-row">
          <button type="button" class="qty-quick" data-course=""
                  aria-pressed="${!context.courseNumber}">Kurssuz</button>
          ${[1, 2, 3, 4, 5].map((course) => `
            <button type="button" class="qty-quick" data-course="${course}"
                    aria-pressed="${context.courseNumber === course}">${course}. kurs</button>`).join('')}
        </div>
      </div>`;
  }

  for (const group of context.product.modifierGroups) {
    const single = group.selectionType === 'Single';
    const required = group.minSelections > 0;
    const rule = required
      ? (single ? 'zorunlu — bir tane seçin' : `zorunlu — en az ${group.minSelections}`)
      : (single ? 'bir tane seçilebilir' : `en fazla ${group.maxSelections}`);
    html += `
      <div class="optgroup" data-group="${escapeHtml(group.modifierGroupId)}">
        <div class="optgroup-head">
          <span class="optgroup-name">${escapeHtml(group.name)}</span>
          <span class="optgroup-rule${required ? ' is-required' : ''}">${escapeHtml(rule)}</span>
        </div>
        <div class="opts">
          ${group.modifiers.map((modifier) => `
            <button type="button" class="opt" data-modifier="${escapeHtml(modifier.modifierId)}"
                    data-group="${escapeHtml(group.modifierGroupId)}" data-single="${single}"
                    aria-pressed="${context.chosen.has(modifier.modifierId)}">
              <span class="opt-box${single ? ' is-round' : ''}">
                <svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg>
              </span>
              <span class="opt-name">${escapeHtml(modifier.name)}</span>
              <span class="opt-delta${modifier.priceDelta ? '' : ' is-free'}">${
                modifier.priceDelta ? `+${escapeHtml(formatMoney(modifier.priceDelta))}` : 'ücretsiz'}</span>
            </button>`).join('')}
        </div>
      </div>`;
  }

  html += `
    <div class="optgroup">
      <div class="optgroup-head"><span class="optgroup-name">Not</span></div>
      <input class="note" type="text" maxlength="200" data-product-note
             placeholder="Mutfağa not (az pişmiş, soğansız…)" aria-label="Mutfağa not">
    </div>`;
  return html;
}

export function chosenModifiers() {
  const context = state.optionsContext;
  return Array.from(context.chosen.values());
}

export function updateProductSheetTotal() {
  const context = state.optionsContext;
  const count = modifierCountFor(context.quantity);
  const extras = chosenModifiers().reduce((sum, modifier) => sum + modifier.priceDelta * count, 0);
  el.optionsFootValue.textContent = formatMoney(context.product.price * context.quantity + extras);
}

export function toggleModifier(button) {
  const context = state.optionsContext;
  const modifierId = button.dataset.modifier;
  const groupId = button.dataset.group;
  const group = context.product.modifierGroups.find((candidate) => candidate.modifierGroupId === groupId);
  const modifier = group.modifiers.find((candidate) => candidate.modifierId === modifierId);

  if (context.chosen.has(modifierId)) {
    context.chosen.delete(modifierId);
  } else {
    if (button.dataset.single === 'true') {
      for (const other of group.modifiers) context.chosen.delete(other.modifierId);
    } else if (group.maxSelections > 0) {
      const chosenInGroup = group.modifiers.filter((candidate) => context.chosen.has(candidate.modifierId)).length;
      if (chosenInGroup >= group.maxSelections) {
        toast(`${group.name} için en fazla ${group.maxSelections} seçim yapılabilir.`, { warning: true });
        return;
      }
    }
    // No quantity is stored or sent. V1-RMD-150 makes it optional exactly so
    // the server can apply its own rule - the ceiling of the line quantity -
    // and this screen has no reason to override it: the waiter never asked
    // for a specific count. The sent line then renders the count the server
    // actually recorded.
    context.chosen.set(modifierId, {
      modifierId,
      name: modifier.name,
      priceDelta: modifier.priceDelta
    });
  }

  el.optionsBody.querySelectorAll('[data-modifier]').forEach((candidate) => {
    candidate.setAttribute('aria-pressed', String(context.chosen.has(candidate.dataset.modifier)));
  });
  updateProductSheetTotal();
}
