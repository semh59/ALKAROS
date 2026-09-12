// ALKAROS Waiter PWA — the "yardım çağır" sheet (V1-WTR-047, step 11/? of
// docs/engineering/garson-refactor-plan.md's Section 2).

import { state, el } from '../state.js';
import { escapeHtml } from '../util.js';
import { openOptions, closeOptions } from '../options-sheet.js';
import { toast } from '../toast.js';
import { apiUrl, api } from '../api.js';

// V1-WTR-014: real-time, reaches every connected manager/supervisor
// device (not cashier — Semih's decision, 2026-09-11: they're busy at
// the till). HelpRequestTypeCatalog (src/Host/Experience/HelpRequests/
// HelpRequestContracts.cs). The codes are the server's; only the
// wording is ours.
const HELP_REQUEST_TYPES = [
  { code: 'Spill', label: 'Döküldü / temizlik gerekiyor' },
  { code: 'Complaint', label: 'Misafir şikayeti' },
  { code: 'Approval', label: 'Onay gerekiyor' },
  { code: 'Other', label: 'Diğer' }
];

export function openHelpRequestSheet() {
  if (!state.table) return;
  state.optionsContext = { reason: null };
  const body = `
    <div class="opts">
      ${HELP_REQUEST_TYPES.map((type) => `
        <button type="button" class="opt" data-reason="${escapeHtml(type.code)}" aria-pressed="false">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
          <span class="opt-name">${escapeHtml(type.label)}</span>
        </button>`).join('')}
    </div>`;
  openOptions('help-request', 'Yardım çağır',
    `${state.table.number} masası için nöbetçi yöneticiye anında bildirim gider`,
    body, 'Çağır', '', '');
  el.optionsConfirm.className = 'btn btn-primary';
  el.optionsConfirm.disabled = true;
}

export async function confirmHelpRequest() {
  const context = state.optionsContext;
  if (!context || !context.reason || !state.table) return;
  el.optionsConfirm.disabled = true;

  const result = await api(apiUrl('/help-requests'), {
    method: 'POST',
    body: { tableId: state.table.id, requestType: context.reason }
  });

  if (!result.ok) {
    // V1-WTR-014: a 429 (same table's 2-minute cooldown still active) is
    // routine, not an error - the earlier call already reached
    // management, this one would only be a duplicate.
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  closeOptions();
  toast('Yardım çağrınız yöneticiye iletildi.');
}
