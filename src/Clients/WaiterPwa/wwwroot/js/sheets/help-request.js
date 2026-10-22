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
    `${state.table.number} masası için nöbetçi yöneticiye bildirim gider; kimse bağlı değilse çağrı kayıtta bekler`,
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
    // A 429 (the table's earlier call is still unanswered) is routine, not an
    // error; the message already says how many seconds to wait.
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  closeOptions();
  // The server counts the managers connected at that moment; with none, the
  // call is only stored, so it must not be reported as delivered.
  if (result.data && result.data.connectedRecipients === 0) {
    toast('Çağrınız kaydedildi; şu an bağlı yönetici yok. Yöneticiye iletildiği anda haber verilecek.');
    return;
  }
  toast('Yardım çağrınız yöneticiye iletildi.');
}
