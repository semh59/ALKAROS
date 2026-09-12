// ALKAROS Waiter PWA — pure utility functions (V1-WTR-037, step 1/? of
// docs/engineering/garson-refactor-plan.md's Section 2 — waiter-app.js
// modularization).
//
// Everything here touches only its own arguments, `document`/`window`
// globals, or `localStorage` — never the app's shared `state` object or the
// `el` DOM-element cache. That is the actual criterion for "belongs in
// util.js" (not merely "the plan's own file listed it here" — a few names
// the plan mentioned, e.g. `seatLabel` (reads `state.tableSeats`) and
// `measureChrome` (reads `el.ribbon`/`el.pendingBanner`), turned out on
// inspection to depend on shared state, so they stay behind for a later
// step (`state.js`/`screens/tables.js`) instead of moving here.

// Escapes for BOTH text and double-quoted attribute contexts.
//
// This used to serialize a text node (`div.textContent = …; return
// div.innerHTML`), which escapes only & < > — a quote passed through
// untouched. Every attribute in this file is double-quoted and several
// carry human-entered text (a product name, a table number, a line note),
// so a product called `Kola" onmouseover="…` closed the attribute and
// injected an event handler that ran in the app's own origin with the
// waiter's session. The stored variant needed no menu access at all: a QR
// guest typed a quote into a special instruction and *Turu tekrarla*
// copied it into the note field's value attribute.
const HTML_ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

export function escapeHtml(value) {
  if (value === null || value === undefined) return '';
  return String(value).replace(/[&<>"']/g, (character) => HTML_ESCAPES[character]);
}

const moneyFormat = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });
const quantityFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 3 });
const clockFormat = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

export function formatMoney(amount) {
  return moneyFormat.format(Number(amount) || 0);
}

// A half portion is 0,5 - the quantity itself, never a separate product or
// a priced option (V1-RMD-146).
export function formatQuantity(quantity) {
  return quantityFormat.format(Number(quantity) || 0);
}

export function formatClock(value) {
  const date = value ? new Date(value) : null;
  return date && !Number.isNaN(date.getTime()) ? clockFormat.format(date) : '';
}

// docs/UI_STYLE_GUIDE.md §3: a raw status code never reaches the screen.
export function describeHttpFailure(status) {
  if (status === 400) return 'İstek doğrulanamadı. Masa ve ürün bilgilerini kontrol edin.';
  if (status === 401) return 'Oturum geçersiz veya süresi doldu. Yeniden giriş yapın.';
  if (status === 403) return 'Bu işlem için yetkiniz yok.';
  if (status === 404) return 'İlgili kayıt bulunamadı.';
  if (status === 409) return 'Kayıt başka bir işlem tarafından değiştirildi. Tekrar deneyin.';
  if (status === 423) return 'Çok fazla hatalı deneme yapıldı.';
  if (status >= 500) return 'Sunucu hatası oluştu. Tekrar deneyin.';
  return 'İstek sunucu tarafından reddedildi. Tekrar deneyin.';
}

// crypto.randomUUID() is secure-context only, so it is undefined over plain
// HTTP on a LAN IP - which is exactly how a waiter phone reaches the stack.
// crypto.getRandomValues() IS available there, so build a v4 UUID from it.
export function randomUUID() {
  if (window.crypto && typeof window.crypto.randomUUID === 'function') {
    return window.crypto.randomUUID();
  }
  const bytes = window.crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

// Each device gets its own terminal id (persisted) so concurrent waiters do
// not share - and revoke - one cashier device session.
export function deviceTerminalId() {
  let id = localStorage.getItem('alkaros_waiter_terminal_id');
  if (!id) {
    id = randomUUID();
    localStorage.setItem('alkaros_waiter_terminal_id', id);
  }
  return id;
}

// V1-WTR-025: a plain label, not a lookup — unlike seats, a course number
// is not resolved against any server-side catalog, it is just an integer
// the waiter assigns while building the draft.
export function courseLabel(courseNumber) {
  return `${courseNumber}. kurs`;
}

export function isFullscreen() {
  return document.fullscreenElement !== null && document.fullscreenElement !== undefined;
}
