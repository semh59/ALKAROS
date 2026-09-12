// ALKAROS Waiter PWA — the profile/settings sheet and the self-view
// shift summary (V1-WTR-052, step 16/? of
// docs/engineering/garson-refactor-plan.md's Section 2).

import { state, el } from '../state.js';
import { escapeHtml, formatMoney, isFullscreen } from '../util.js';
import { featureEnabled } from '../features.js';
import { refreshPushState } from '../push.js';
import { openOptions } from '../options-sheet.js';
import { apiUrl, api } from '../api.js';

export function openProfileSheet() {
  void refreshPushState().then(() => {
    const body = `
      <div class="opts">
        <button type="button" class="opt" data-profile="fullscreen">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-expand"/></svg></span>
          <span class="opt-name">${isFullscreen() ? 'Tam ekrandan çık' : 'Tam ekran'}</span>
        </button>
        <button type="button" class="opt" data-profile="push">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-bell"/></svg></span>
          <span class="opt-name">${state.pushEnabled
            ? 'Arka plan bildirimini kapat'
            : 'Uygulama kapalıyken de bildir'}</span>
        </button>
        <button type="button" class="opt" data-profile="lock">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-lock"/></svg></span>
          <span class="opt-name">${state.pinArmed ? 'Ekranı şimdi kilitle' : 'Ekran kilidini kur'}</span>
        </button>
        ${state.pinArmed ? `
        <button type="button" class="opt" data-profile="pin-off">
          <span class="opt-box is-round"></span>
          <span class="opt-name">Ekran kilidini kaldır</span>
        </button>` : ''}
        <button type="button" class="opt" data-profile="transfer-server">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-move"/></svg></span>
          <span class="opt-name">Açık masaları devret</span>
        </button>
        ${featureEnabled('shiftSummary') ? `
        <button type="button" class="opt" data-profile="shift-summary">
          <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-seats"/></svg></span>
          <span class="opt-name">Vardiya özetim</span>
        </button>` : ''}
        <button type="button" class="opt" data-profile="signout">
          <span class="opt-box is-round"></span>
          <span class="opt-name">Oturumu kapat</span>
        </button>
      </div>`;
    openOptions('profile', (state.user && state.user.displayName) || 'Personel',
      'Bu cihaz için ayarlar', body, '', '', '');
    el.optionsConfirm.hidden = true;
  });
}

// V1-WTR-021: garson-karsilastirma idea #9, "yalnızca kendine görünen
// vardiya özeti". The server already scopes /my-shift-summary to the
// calling session (RequireCashierSessionAsync) - there is no manager
// view or other-waiter lookup to accidentally build here.
export async function openShiftSummarySheet() {
  openOptions('shift-summary', 'Vardiya özetim', 'Bugün (UTC gün başlangıcından beri)',
    '<div class="empty">Yükleniyor…</div>', '', '', '');
  el.optionsConfirm.hidden = true;

  const result = await api(apiUrl('/orders/my-shift-summary'));
  if (!result.ok) {
    el.optionsBody.innerHTML = `<div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>${escapeHtml(result.message || 'Vardiya özeti alınamadı.')}</span>
    </div>`;
    return;
  }

  const summary = result.data;
  el.optionsBody.innerHTML = `
    <div class="opts">
      <div class="shift-summary-row">
        <span class="shift-summary-label">Satış toplamım</span>
        <span class="shift-summary-value">${formatMoney(summary.salesTotal)}</span>
      </div>
      <div class="shift-summary-row">
        <span class="shift-summary-label">Kullandığım ikram bütçesi</span>
        <span class="shift-summary-value">${formatMoney(summary.compUsed)}</span>
      </div>
      <div class="shift-summary-row">
        <span class="shift-summary-label">Bahşiş havuzu payım</span>
        <span class="shift-summary-value">${formatMoney(summary.tipPoolShare)}</span>
      </div>
    </div>
    <p class="shift-summary-note">
      Bahşiş havuzu payı: bugün toplanan ${formatMoney(summary.tipPoolTotal)} bahşiş,
      bugün en az bir sipariş alan ${summary.waitersWorkedToday} garson arasında eşit bölünür.
    </p>`;
}
