// ALKAROS Waiter PWA — the party-size ("kaç kişi?") sheet (V1-WTR-044,
// step 8/? of docs/engineering/garson-refactor-plan.md's Section 2, the
// plan's own second module in its 2.4 ordering).
//
// confirmPartySize() stays in waiter-app.js for now, not here: it calls
// renderBill(), which is bill.js's concern and does not exist as a module
// yet (the plan's own 2.4 ordering puts bill.js right after this step).
// The +/- stepper's click handler (data-party-step) is part of
// onOptionsBodyClick's shared sheet-routing switch and stays there too.

import { state, el } from '../state.js';
import { escapeHtml } from '../util.js';
import { openOptions } from '../options-sheet.js';

// V1-WTR-015: garson-karsilastirma idea, "kuver" - party size is only
// editable/settable before the table's FIRST round is ever sent; the
// server only ever persists it at that instant
// (CreateTableDraftRequest.PartySize's own doc comment). Correcting it
// afterwards is out of that task's scope.

export function openPartySizeSheet() {
  if (!state.table || state.order) return;
  state.optionsContext = { partySize: state.draftPartySize || 2 };
  openOptions('party-size', 'Kaç kişi?', `${state.table.number} masası`,
    partySizeSheetHtml(state.optionsContext.partySize), 'Tamam', '', '');
  el.optionsConfirm.className = 'btn btn-primary';
}

export function partySizeSheetHtml(value) {
  return `
    <div class="stepper stepper-lg">
      <button type="button" data-party-step="-" aria-label="Azalt">−</button>
      <span class="qty" id="partySizeValue">${escapeHtml(String(value))}</span>
      <button type="button" data-party-step="+" aria-label="Artır">+</button>
    </div>`;
}
