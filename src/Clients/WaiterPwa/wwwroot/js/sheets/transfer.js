// ALKAROS Waiter PWA — moving one table's check to another empty table,
// and handing every open check off to a colleague (V1-WTR-048, step 12/?
// of docs/engineering/garson-refactor-plan.md's Section 2).
//
// confirmTransfer() calls openTable() (tables.js) — only possible now that
// openTable moved there itself (this same task), once bill.js and menu.js
// existed to resolve what it used to orchestrate across.

import { state, el } from '../state.js';
import { escapeHtml } from '../util.js';
import { openTable, loadTables } from '../screens/tables.js';
import { openOptions, closeOptions } from '../options-sheet.js';
import { toast } from '../toast.js';
import { featureEnabled } from '../features.js';
import { apiUrl, api } from '../api.js';

export function openTransferSheet() {
  if (!state.table) return;
  const targets = state.tables.filter((table) =>
    table.id !== state.table.id && table.status === 'available');

  const body = targets.length === 0
    ? '<div class="empty">Şu anda boş masa yok.</div>'
    : `<div class="opts">${targets.map((table) => `
        <button type="button" class="opt" data-target="${escapeHtml(table.id)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(table.number)} masası</span>
          <span class="opt-code">${escapeHtml(table.seats)} kişilik</span>
        </button>`).join('')}</div>`;

  state.optionsContext = { targetId: null };
  openOptions('transfer', 'Masa değiştir',
    `${state.table.number} masasındaki sipariş ve hesap taşınır`, body, 'Taşı', '', '');
  el.optionsConfirm.className = 'btn btn-primary';
  el.optionsConfirm.disabled = true;
}

export async function confirmTransfer() {
  const context = state.optionsContext;
  if (!context || !context.targetId) return;
  const target = state.tables.find((table) => table.id === context.targetId);
  if (!target) return;

  el.optionsConfirm.disabled = true;
  const result = await api(apiUrl('/table-management/transfers'), {
    method: 'POST',
    body: {
      sourceTableId: state.table.id,
      expectedSourceRowVersion: state.table.rowVersion,
      targetTableId: target.id,
      expectedTargetRowVersion: target.rowVersion,
      reason: 'Misafir masa değiştirdi'
    }
  });

  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }
  const from = state.table.number;
  closeOptions();
  await loadTables();
  const moved = state.tables.find((table) => table.id === target.id);
  if (moved) await openTable(moved.id, false);
  toast(`${from} masası ${target.number} masasına taşındı.`);
}

// V1-RMD-111/V1-RMD-177. Different from openTransferSheet above (that
// moves ONE table's order to a different, empty table); this reassigns
// EVERY order this waiter currently serves to a colleague at once — the
// "I'm going on break/leaving" move. orders.transfer-server (every role
// holds it) covers only one's own orders; deliberately not offering the
// orders.transfer-server-any (someone else's) variant here — that is a
// cashier/supervisor action, out of this client's scope.

export async function openTransferServerSheet() {
  if (!state.user) return;
  const result = await api(apiUrl('/orders/staff'));
  if (!result.ok) { closeOptions(); toast(result.message, { warning: true }); return; }

  const staff = Array.isArray(result.data) ? result.data : [];
  const body = staff.length === 0
    ? '<div class="empty">Devredilecek başka personel yok.</div>'
    : `<div class="opts">${staff.map((person) => `
        <button type="button" class="opt" data-target="${escapeHtml(person.userId)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(person.displayName)}</span>
        </button>`).join('')}</div>`;

  // V1-WTR-013: optional context for the receiving waiter — "table 5 is
  // waiting on dessert, table 8 complained" — shown to them once, the
  // first time their client pops it (see popHandoffNoteIfAny below).
  // V1-SET-004: omitted outright when this deployment has hand-off notes
  // turned off — the server already silently drops it either way, but a
  // hidden field never even looks offered.
  const noteField = featureEnabled('shiftHandoffNotes') ? `
    <label class="hint" for="handoffNoteInput">Devir notu (isteğe bağlı)</label>
    <input class="note" type="text" id="handoffNoteInput" maxlength="200"
           placeholder="Örn. 5 nolu masa tatlı bekliyor">` : '';

  state.optionsContext = { targetId: null };
  openOptions('transfer-server', 'Masaları devret',
    // V1-RMD-178: found by independent review of V1-RMD-177 — this used
    // to say "bu cihazda açık olan" (open on this device), but
    // TransferServingUserAsync reassigns EVERY order attributed to this
    // user, on every device/terminal — device-independent, no undo. The
    // text now says what the action actually does.
    'Üzerinizdeki TÜM açık masalar (bu cihazda olsun olmasın) seçtiğiniz kişiye geçer — geri alınamaz',
    body + noteField, 'Devret', '', '');
  el.optionsConfirm.className = 'btn btn-primary';
  el.optionsConfirm.disabled = true;
}

export async function confirmTransferServer() {
  const context = state.optionsContext;
  if (!context || !context.targetId || !state.user) return;
  el.optionsConfirm.disabled = true;

  const noteField = el.optionsBody.querySelector('#handoffNoteInput');
  const handoffNote = noteField ? noteField.value.trim() : '';

  const result = await api(apiUrl('/orders/transfer-server'), {
    method: 'POST',
    body: { fromUserId: state.user.userId, toUserId: context.targetId, handoffNote: handoffNote || null }
  });

  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }
  closeOptions();
  const count = result.data && result.data.ordersReassigned;
  toast(count > 0 ? `${count} masa devredildi.` : 'Devredilecek açık masa yoktu.');
  await loadTables();
}
