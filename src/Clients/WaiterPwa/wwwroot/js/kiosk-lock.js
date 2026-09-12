// ALKAROS Waiter PWA — full screen / wake lock and the PIN kiosk lock
// (V1-WTR-042, step 6/? of docs/engineering/garson-refactor-plan.md's
// Section 2). The plan's own file listing put this inside auth.js, but on
// inspection it turned out to be a large, render-heavy concern of its own
// (V1-WTR-039 deferred it for exactly this reason) — its own module now
// that toast.js and options-sheet.js, the two pieces it needed that did
// not exist yet at that point, both do.

import { state, el } from './state.js';
import { isFullscreen } from './util.js';
import { toast } from './toast.js';
import { openOptions, closeOptions } from './options-sheet.js';
import { trapBackgroundExcept, releaseTrap, showLogin } from './auth.js';
import { api } from './api.js';

const IDLE_LOCK_MS = 3 * 60 * 1000;

// ══ Full screen ════════════════════════════════════════════════════
// Semih's own "tam ekranda çıkmayı zorlaştırmak". A web page cannot pin
// itself the way Android Ekran Sabitleme or iOS Rehberli Erişim can - that
// is an operating-system setting. What it can do is take the whole screen,
// keep it awake, and make leaving cost something: if a PIN is set, dropping
// out of full screen locks the device.

export async function requestWakeLock() {
  if (!('wakeLock' in navigator)) return;
  try {
    state.wakeLock = await navigator.wakeLock.request('screen');
  } catch {
    // Denied, or the tab is not visible. The screen simply dims as usual.
    state.wakeLock = null;
  }
}

export async function releaseWakeLock() {
  try { if (state.wakeLock) await state.wakeLock.release(); }
  catch { /* already gone */ }
  state.wakeLock = null;
}

export async function toggleFullscreen() {
  try {
    if (isFullscreen()) {
      await document.exitFullscreen();
      await releaseWakeLock();
      return;
    }
    await document.documentElement.requestFullscreen({ navigationUI: 'hide' });
    await requestWakeLock();
    // Landscape and portrait are both legitimate on a tablet, so the
    // orientation is deliberately left unlocked (V1-RMD-145 removed the
    // lock from the manifest for the same reason).
  } catch {
    toast('Tam ekrana geçilemedi.', { warning: true });
  }
}

export function onFullscreenChange() {
  if (isFullscreen()) return;
  void releaseWakeLock();
  // Leaving full screen is how someone gets out of the app. With a PIN set
  // that hands them the lock screen instead of the order list.
  if (state.pinArmed && !state.locked) lockScreen();
}

// ══ PIN and the kiosk lock ═══════════════════════════════════════════

// Setting a PIN needs the current password on top of the session, so a
// device left unlocked on a table cannot have one planted on it.
export function openPinSheet(removing) {
  state.optionsContext = { removing: !!removing };
  const body = `
    <div class="callout">
      <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
      <span>${removing
        ? 'Kilidi kaldırmak için şifrenizi doğrulayın.'
        : 'Ekran 3 dakika boştayken kilitlenir. Açmak için bu PIN yeterlidir; şifreniz istenmez.'}</span>
    </div>
    <div class="optgroup">
      <div class="optgroup-head"><span class="optgroup-name">Şifreniz</span></div>
      <input class="note" type="password" data-pin-password autocomplete="current-password" aria-label="Şifreniz">
    </div>
    ${removing ? '' : `
    <div class="optgroup">
      <div class="optgroup-head"><span class="optgroup-name">Yeni PIN</span>
        <span class="optgroup-rule">en az 4 rakam</span></div>
      <input class="note" type="password" inputmode="numeric" data-pin-code
             autocomplete="one-time-code" aria-label="Yeni PIN">
    </div>`}`;
  openOptions('pin', removing ? 'Kilidi kaldır' : 'Ekran kilidi', '', body,
    removing ? 'Kaldır' : 'Kur', '', '');
  el.optionsConfirm.className = removing ? 'btn btn-danger' : 'btn btn-primary';
  el.optionsConfirm.disabled = false;
}

export async function confirmPin() {
  const removing = state.optionsContext && state.optionsContext.removing;
  const password = el.optionsBody.querySelector('[data-pin-password]').value || '';
  const codeField = el.optionsBody.querySelector('[data-pin-code]');
  const code = codeField ? (codeField.value || '') : null;

  if (!password) { toast('Şifrenizi girin.', { warning: true }); return; }
  if (!removing && (code.length < 4 || !/^\d+$/.test(code))) {
    toast('PIN en az 4 rakam olmalı.', { warning: true });
    return;
  }

  el.optionsConfirm.disabled = true;
  const result = await api(`/api/v1/auth/pin?terminalId=${state.terminalId}`, {
    method: 'POST',
    body: { currentPassword: password, pin: removing ? null : code }
  });
  if (!result.ok) {
    toast(result.message, { warning: true });
    el.optionsConfirm.disabled = false;
    return;
  }

  // A local flag only decides whether this device arms its idle timer; the
  // server stays the authority on whether the PIN itself is valid.
  state.pinArmed = !removing;
  localStorage.setItem('alkaros_waiter_pin_armed', state.pinArmed ? '1' : '0');
  closeOptions();
  toast(removing ? 'Ekran kilidi kaldırıldı.' : 'Ekran kilidi kuruldu.');
}

let idleTimer = null;

export function resetIdleTimer() {
  window.clearTimeout(idleTimer);
  if (!state.pinArmed || state.locked || el.loginOverlay.hidden === false) return;
  idleTimer = window.setTimeout(lockScreen, IDLE_LOCK_MS);
}

export function lockScreen() {
  if (!state.pinArmed || state.locked) return;
  state.locked = true;
  state.pinBuffer = '';
  el.lockSub.textContent = 'PIN kodunuzu girin';
  el.pinDots.classList.remove('is-wrong');
  renderPinDots();
  el.lockOverlay.hidden = false;
  // Deliberately no Escape-to-close and no data-pin keys reachable from
  // outside: this overlay exists specifically so a keyboard cannot walk
  // around it.
  trapBackgroundExcept(el.lockOverlay);
  el.pinKeys.querySelector('[data-pin="1"]')?.focus();
}

export function renderPinDots() {
  el.pinDots.innerHTML = Array.from({ length: Math.max(4, state.pinBuffer.length) },
    (unused, index) => `<span class="${index < state.pinBuffer.length ? 'is-filled' : ''}"></span>`).join('');
}

export function renderPinPad() {
  const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9'];
  el.pinKeys.innerHTML = keys.map((key) =>
    `<button type="button" class="pin-key" data-pin="${key}">${key}</button>`).join('')
    + '<button type="button" class="pin-key" data-pin="del" aria-label="Sil">⌫</button>'
    + '<button type="button" class="pin-key" data-pin="0">0</button>'
    + '<button type="button" class="pin-key" data-pin="ok" aria-label="Aç">✓</button>';
}

export async function submitPin() {
  if (state.pinBuffer.length < 4) return;
  const result = await api(`/api/v1/auth/unlock?terminalId=${state.terminalId}`, {
    method: 'POST',
    body: { pin: state.pinBuffer }
  });

  if (result.ok) {
    state.locked = false;
    state.pinBuffer = '';
    el.lockOverlay.hidden = true;
    releaseTrap();
    resetIdleTimer();
    return;
  }

  if (result.status === 409) {
    // The server says this account has no PIN, so the flag on this device
    // is stale - drop the lock rather than trapping the waiter behind it.
    state.pinArmed = false;
    localStorage.setItem('alkaros_waiter_pin_armed', '0');
    state.locked = false;
    el.lockOverlay.hidden = true;
    releaseTrap();
    toast('Bu hesapta PIN tanımlı değil, kilit kaldırıldı.', { warning: true });
    return;
  }

  state.pinBuffer = '';
  renderPinDots();
  el.pinDots.classList.add('is-wrong');
  window.setTimeout(() => el.pinDots.classList.remove('is-wrong'), 400);
  el.lockSub.textContent = result.status === 423
    ? 'Çok fazla deneme. Kullanıcı adı ve şifreyle girin.'
    : 'PIN hatalı, tekrar deneyin.';
  if (result.status === 423) showLogin();
}
