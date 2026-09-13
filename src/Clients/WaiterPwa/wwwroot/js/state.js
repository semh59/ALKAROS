// ALKAROS Waiter PWA — shared mutable state, and the DOM-element cache
// every screen reads from (V1-WTR-038, step 2/? of
// docs/engineering/garson-refactor-plan.md's Section 2).
//
// What used to be two closure-captured `const` bindings any function inside
// the old single IIFE could silently reach into are now explicit imports —
// `import { state, el } from './state.js'` names exactly which shared
// mutable container a module touches, instead of a reader having to scan
// the whole file to discover it.
//
// Both are still ordinary mutable objects: an ES module's exported `const`
// binding is immutable as a BINDING, not as the object it points to —
// `state.draft = []` from any importer still mutates the one shared
// instance every other importer sees, exactly like the old closure did.
// Nothing about how callers read/write `state`/`el` changes here, only
// where the two are declared.

import { deviceTerminalId } from './util.js';

// V1-RMD-170: see state.draftsByTable's own comment below. Stored as an
// array of [tableId, {number, lines}] pairs since a Map is not directly
// JSON-serializable.
function loadDraftsByTable() {
  try {
    const raw = localStorage.getItem('alkaros_waiter_drafts_by_table');
    return raw ? new Map(JSON.parse(raw)) : new Map();
  } catch {
    // Corrupt/foreign localStorage content must never crash startup -
    // worst case the held drafts are gone, same as before this fix.
    return new Map();
  }
}

// V1-RMD-183: found by the 2026-09-12 five-agent independent Garson
// audit — offlineQueue/failedOrders read their own localStorage keys with
// a bare JSON.parse, unlike loadDraftsByTable right above (and its own
// V1-RMD-170 lesson: "corrupt/foreign localStorage content must never
// crash startup"). This module is imported before almost anything else,
// so a thrown SyntaxError here (a manual edit, a stale schema from an
// old app version, another extension writing to the same origin) failed
// the whole app at import time - every screen, not just the offline
// queue. Same fallback shape loadDraftsByTable already uses.
function loadJsonArray(key) {
  try {
    const raw = localStorage.getItem(key);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

// ══ State ══════════════════════════════════════════════════════════
// Everything under `server` is a copy of a DTO. Everything under `draft` is
// the round being composed on this device and not yet sent.

export const state = {
  terminalId: deviceTerminalId(),
  isOnline: navigator.onLine,
  offlineDisabled: false,
  offlineDisabledReason: null,
  user: null,
  capabilities: [],
  // V1-SET-004: per-deployment on/off for every optional Garson feature
  // (/runtime-configuration's own garsonFeatures object) - fetched once
  // in start(). Missing/unfetched reads as enabled (featureEnabled()'s
  // own default) so a network hiccup or an older server never silently
  // disables something instead of just not knowing about the setting.
  features: {},

  zones: [],
  tables: [],
  products: [],
  categories: [],

  activeZone: 'all',
  activeCategory: 'all',
  search: '',

  table: null,
  order: null,
  draft: [],
  // V1-WTR-015: only meaningful before the table's first round is ever
  // sent (state.order is still null) - once a real Order exists,
  // order.partySize is the source of truth and this is ignored. Reset
  // whenever a different table is opened (see openTable()).
  draftPartySize: null,
  // V1-WTR-022: the currently-open table's floor-plan seats
  // ({id, number, label}), empty when the table has no floor-plan layout
  // at all - seat assignment is then simply not offered, never an error.
  tableSeats: [],
  // Unsent rounds for tables the waiter stepped away from, keyed by table
  // id. A waiter checking another table mid-order is ordinary; losing what
  // they typed is not.
  //
  // V1-RMD-170: found by the 2026-09-10 Garson audit — this used to be
  // in-memory only, so a page reload (an accidental pull-to-refresh, the
  // browser reclaiming memory, a crash) silently erased whatever a
  // waiter had typed but not yet sent, with no warning and nothing to
  // undo. Restored from localStorage at startup the same way
  // offlineQueue/failedOrders already are; loadDraftsByTable/
  // persistDraftsByTable below keep it in sync every time it changes.
  draftsByTable: loadDraftsByTable(),
  // Bumped whenever the current round is cleared or sent, so a stale undo
  // cannot resurrect a line into a round that no longer exists.
  draftEpoch: 0,

  pending: [],
  offlineQueue: loadJsonArray('alkaros_waiter_offline_queue'),
  failedOrders: loadJsonArray('alkaros_waiter_failed_orders'),

  sendInFlight: false,
  optionsMode: null,
  optionsContext: null,
  pinArmed: localStorage.getItem('alkaros_waiter_pin_armed') === '1',
  locked: false,
  pinBuffer: '',
  pushEnabled: false,
  wakeLock: null
};

export const el = {};
[
  'ribbon', 'ribbonText', 'ribbonQueue', 'userName', 'userRole', 'userInitials', 'btnProfile',
  'pendingBanner', 'pendingTitle', 'pendingSub',
  'tablesScreen', 'menuScreen', 'zoneChips', 'tablesGrid',
  'btnMenuBack', 'menuTableName', 'menuTableSub', 'productSearch', 'categoryChips', 'productList',
  'cartBar', 'cartCount', 'cartTotal', 'btnOpenBill', 'btnSendFromMenu',
  'billBackdrop', 'billSheet', 'billTitle', 'billSub', 'billBody', 'billTotal',
  'btnAddItems', 'btnPartySize', 'btnHelpRequest', 'btnMoveTable', 'btnSendToCashier', 'billClose', 'btnSendFromBill',
  'optionsBackdrop', 'optionsSheet', 'optionsTitle', 'optionsSub', 'optionsBody',
  'optionsClose', 'optionsConfirm', 'optionsFootLabel', 'optionsFootValue',
  'toasts', 'loginOverlay', 'loginForm', 'loginUsername', 'loginPassword', 'loginError', 'loginSubmit',
  'lockOverlay', 'lockSub', 'pinDots', 'pinKeys'
].forEach((id) => { el[id] = document.getElementById(id); });

// V1-RMD-170: the write side of state.draftsByTable, reunited here with
// loadDraftsByTable above (its read side) rather than left in
// offline-queue.js as originally planned — on inspection, menu.js's own
// afterDraftChange() needed it, and offline-queue.js does not exist yet.
export function persistDraftsByTable() {
  localStorage.setItem(
    'alkaros_waiter_drafts_by_table',
    JSON.stringify([...state.draftsByTable]));
}

// Kept here rather than in screens/tables.js (which owns state.tableSeats
// itself): bill.js already needs this to render a line's seat label, and
// tables.js needs to import openTable's own dependencies FROM bill.js
// (V1-WTR-048) — tables.js -> bill.js -> tables.js would have been a real
// circular import if this stayed on the tables.js side of it.
export function seatLabel(seatId) {
  const seat = state.tableSeats.find((candidate) => candidate.id === seatId);
  return seat ? seat.label : null;
}

// The tablet bill column starts below whatever chrome is currently showing;
// the guest banner appears and disappears, so this is measured, not
// assumed. Kept here (an `el`-only DOM reader, like the rest of this file)
// rather than in whichever module happens to call it first.
export function measureChrome() {
  const header = document.querySelector('.app-header');
  let height = (header ? header.offsetHeight : 0) + (el.ribbon ? el.ribbon.offsetHeight : 0);
  if (!el.pendingBanner.hidden) height += el.pendingBanner.offsetHeight;
  document.documentElement.style.setProperty('--chrome-height', `${height}px`);
}

// V1-RMD-172: one accurate Turkish sentence per real reason offline mode
// can end up disabled, instead of a single "HTTPS gerekli" that was
// wrong whenever the actual cause was something else.
const OFFLINE_DISABLED_REASONS = {
  insecure: 'Çevrimdışı mod kapalı — güvenli bağlantı (HTTPS) gerekli',
  unsupported: 'Çevrimdışı mod bu tarayıcıda desteklenmiyor',
  'registration-failed': 'Çevrimdışı mod kurulamadı — sayfayı yenileyin',
  unknown: 'Çevrimdışı mod kapalı'
};

// Kept here (not offline-queue.js, where the caller that actually cares
// about the queue count lives) because it is genuinely a state reader for
// four different pieces of state (isOnline, offlineDisabled, offlineQueue,
// failedOrders), not specifically an offline-queue concern - waiter-app.js
// itself (registerOfflineWorker, the online/offline listeners) is still
// this function's biggest caller.
export function renderRibbon() {
  const offline = state.offlineDisabled || !state.isOnline;
  el.ribbon.classList.toggle('is-offline', offline);
  if (state.offlineDisabled) {
    el.ribbonText.textContent = OFFLINE_DISABLED_REASONS[state.offlineDisabledReason]
      || OFFLINE_DISABLED_REASONS.unknown;
  } else if (state.isOnline) {
    el.ribbonText.textContent = 'Bağlı';
  } else {
    el.ribbonText.textContent = 'Bağlantı yok — siparişler kuyrukta bekliyor';
  }

  const waiting = state.offlineQueue.length;
  const failed = state.failedOrders.length;
  const parts = [];
  if (waiting > 0) parts.push(`${waiting} bekleyen`);
  if (failed > 0) parts.push(`${failed} hatalı`);
  el.ribbonQueue.textContent = parts.join(' • ');
  // V1-RMD-171: found by the 2026-09-10 Garson audit — the count used
  // to be the whole story; nothing said which table, what was in it, or
  // gave a way to clear it. Now a real button into a real list.
  el.ribbonQueue.hidden = waiting === 0 && failed === 0;
  el.ribbonQueue.setAttribute('aria-label',
    `${parts.join(', ')} - sipariş kuyruğunu göster`);
  measureChrome();
}
