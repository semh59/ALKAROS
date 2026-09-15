// ALKAROS Waiter PWA — sending a round, and the offline retry queue behind
// it (V1-WTR-053, the final module of
// docs/engineering/garson-refactor-plan.md's Section 2, deliberately last
// per the plan's own explicit risk warning: "burada bir hata sessizce
// sipariş kaybına yol açabilir" — a bug here can silently lose an order).
//
// Every export below is exercised by tests\E2E\WaiterPwa\specs\
// 05-load-and-timing.spec.js's own load test (6 concurrent waiters
// sending real rounds) as well as every ordering spec — this module's
// real behavior is broadly covered, unlike push.js/kiosk-lock.js's own
// documented E2E gaps.

import { state, el, renderRibbon } from './state.js';
import { randomUUID } from './util.js';
import { apiUrl, api } from './api.js';
import { toast } from './toast.js';
import { loadOrder, renderBill } from './sheets/bill.js';
import { loadTables, showScreen } from './screens/tables.js';
import { afterDraftChange } from './screens/menu.js';

// V1-RMD-160: waiterName and createdAt used to be sent here but nothing
// on the server ever read either (found by the 2026-09-10 Garson audit).
// The real actor is already attributed server-side via
// Order.ServingUserId, from the session this request already carries;
// the real timestamp is server-authoritative (DateTimeOffset.UtcNow at
// the point the draft is created), same as everywhere else in this
// system — a client clock is never the source of truth for it. Removed
// rather than wired in.
function draftToPayload(items) {
  return {
    id: randomUUID(),
    tableId: state.table.id,
    tableNumber: state.table.number,
    // V1-WTR-015: only takes effect server-side on this table's FIRST
    // round (CreateTableDraftRequest's own doc comment) - sending it on
    // every later round too is harmless, not a correction.
    partySize: state.draftPartySize || null,
    items: (items || state.draft).map((line) => ({
      // A stable per-line id makes a retried draft submission idempotent
      // server-side instead of appending a duplicate line.
      id: line.id,
      productId: line.productId,
      productName: line.name,
      quantity: line.quantity,
      unitPrice: line.price,
      // V1-RMD-147: ids only. The price of an option is the catalog's
      // answer and the count is the server's own rule (V1-RMD-150 leaves
      // the field optional for exactly that) - neither is this client's to
      // assert.
      modifiers: line.modifiers.map((modifier) => ({ modifierId: modifier.modifierId })),
      specialInstructions: line.note || null,
      // V1-WTR-022: the server only ever trusts this once it re-validates
      // the seat actually belongs to this table (OrderManagementStore) -
      // a stale/foreign id here is simply ignored server-side, never an
      // error this client needs to pre-check.
      seatId: line.seatId || null,
      courseNumber: line.courseNumber || null
    }))
  };
}

// V1-RMD-163: found by the 2026-09-10 Garson audit — an
// X-Idempotency-Key header used to be sent here too, carrying the exact
// same value as payload.id in the body. No endpoint anywhere ever reads
// that header (grep confirmed); the real, working idempotency
// protection is payload.id itself, which the server persists as
// Order.SourceReferenceId behind a partial unique index (V1-RMD-123).
// Removed the header as a pointless duplicate rather than wiring up a
// second mechanism for the same value.
async function postOrder(payload) {
  const draft = await api(apiUrl('/orders/table-draft'), { method: 'POST', body: payload });
  if (!draft.ok) return draft;

  // The draft alone never reaches the kitchen; the submit is what dispatches
  // it. The operation id identifies THIS ROUND: `payload.id` is generated
  // once per round and resent unchanged on every retry of it, so a retry
  // replays and the next round is a new operation.
  //
  // It used to be `${orderId}:submit`, which was right only while one order
  // meant one submission. Once a check started taking a second round
  // (V1-ORD-006) every round on that check reused the same key, and the
  // second one came back 409 IDEMPOTENCY_KEY_REUSED — the food never
  // reached the kitchen.
  // Found by the WaiterPwa E2E audit (2026-09-12): a bare `headers,`
  // shorthand property here referenced no variable in scope at all - a
  // leftover from V1-RMD-163's removal of a duplicate X-Idempotency-Key
  // header (that removal deleted the header's VALUE but missed this one
  // remaining reference to it). Evaluating the request options object
  // threw `ReferenceError: headers is not defined` synchronously, before
  // fetch() ever ran - table-draft always succeeded, but submit-draft
  // never even attempted, silently. Every single "Gönder" click was
  // broken: the order never reached the kitchen, with no error shown to
  // the waiter (sendDraft()'s try/finally has no catch, so the thrown
  // error just propagated out of the click handler unseen). No unit or
  // HTTP test catches this class of bug - none of them execute this
  // actual browser script; only running it in a real browser does.
  return api(apiUrl(`/orders/${draft.data.orderId}/submit-draft`), {
    method: 'POST',
    body: {
      orderId: draft.data.orderId,
      expectedRowVersion: draft.data.rowVersion,
      operationId: `${draft.data.orderId}:${payload.id}`
    }
  });
}

function removeSentDraftLines(sentItems) {
  const sentIds = new Set(sentItems.map((line) => line.id));
  state.draft = state.draft.filter((line) => !sentIds.has(line.id));
  state.draftEpoch += 1;
}

// ══ Offline queue ══════════════════════════════════════════════════

export function persistQueue() {
  localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
  localStorage.setItem('alkaros_waiter_failed_orders', JSON.stringify(state.failedOrders));
  renderRibbon();
}

function queueOrder(payload) {
  state.offlineQueue.push(Object.assign({}, payload, { queuedAt: new Date().toISOString() }));
  persistQueue();
  scheduleQueueRetry();
}

// A bulk reconnect (router reset, a shift-start scramble) can drop several
// rounds into the queue before any of them flush. Sending them back in raw
// arrival order treats a dessert round exactly like a starter round, even
// though the kitchen would rather see starters first regardless of which
// waiter happened to queue theirs a few seconds earlier. Mirrors the
// server's own FireRound priority (TableDraftService.cs: the round's
// earliest courseNumber, lower first) so the queue's send order agrees with
// what the kitchen would do with the same rounds anyway; queuedAt (oldest
// first) only breaks a tie within the same course. A round with no
// course-tagged items (course management off, or every line unassigned)
// sorts as course 0 — first, not last: there is no signal to deprioritize
// it by, and in the common case (the whole deployment has course
// management off) every queued round shares that same value, so this never
// actually reorders anything.
function roundPriority(payload) {
  const courseNumbers = (payload.items || [])
    .map((item) => item.courseNumber)
    .filter((value) => typeof value === 'number');
  const minCourse = courseNumbers.length > 0 ? Math.min(...courseNumbers) : 0;
  return { minCourse, queuedAt: payload.queuedAt || '' };
}

export function sortQueueByPriority(queue) {
  return queue.slice().sort((a, b) => {
    const pa = roundPriority(a);
    const pb = roundPriority(b);
    if (pa.minCourse !== pb.minCourse) return pa.minCourse - pb.minCourse;
    if (pa.queuedAt < pb.queuedAt) return -1;
    if (pa.queuedAt > pb.queuedAt) return 1;
    return 0;
  });
}

// The queue used to be flushed only by the browser's `online` event and by
// start(). But the commonest way into the queue is not going offline at
// all — it is the server answering 5xx while the network is perfectly up,
// and in that state `online` never fires. A round queued that way sat in
// localStorage for the rest of the shift and the kitchen never saw it.
//
// So the queue now retries itself, backing off so a server that is down
// does not get hammered, and stops as soon as the queue empties.
const QUEUE_RETRY_MIN_MS = 15000;
const QUEUE_RETRY_MAX_MS = 5 * 60 * 1000;
let queueRetryTimer = null;
let queueRetryDelay = QUEUE_RETRY_MIN_MS;
let flushInFlight = false;

export function scheduleQueueRetry() {
  window.clearTimeout(queueRetryTimer);
  if (state.offlineQueue.length === 0) {
    queueRetryDelay = QUEUE_RETRY_MIN_MS;
    return;
  }
  queueRetryTimer = window.setTimeout(() => {
    queueRetryDelay = Math.min(queueRetryDelay * 2, QUEUE_RETRY_MAX_MS);
    void flushQueue();
  }, queueRetryDelay);
}

export async function flushQueue() {
  if (state.offlineQueue.length === 0 || !state.isOnline) return;
  // Two overlapping flushes would send the same payload twice. The server
  // is idempotent on the submission id, so this is a courtesy rather than
  // the last line of defence — but it also keeps the ribbon honest.
  if (flushInFlight) return;
  flushInFlight = true;

  try {
    for (const payload of sortQueueByPriority(state.offlineQueue)) {
      const result = await postOrder(payload);
      if (result.ok) {
        state.offlineQueue = state.offlineQueue.filter((queued) => queued.id !== payload.id);
        // A success means the server is back; drop the backoff.
        queueRetryDelay = QUEUE_RETRY_MIN_MS;
      } else if (result.status === 429) {
        // Rate limited while draining a long queue. Retryable, and filing
        // it as permanently failed would destroy the round.
        break;
      } else if (result.status >= 400 && result.status < 500) {
        // A 4xx will never succeed on retry, but the order is never destroyed:
        // it moves to the failed list and the ribbon keeps saying so.
        state.offlineQueue = state.offlineQueue.filter((queued) => queued.id !== payload.id);
        state.failedOrders.push(Object.assign({}, payload, { rejectedAt: new Date().toISOString(), error: result.message }));
        console.error('Order rejected by server:', payload.id, result.status, result.message);
      } else {
        // Temporary failure: keep the rest queued and stop trying for now.
        break;
      }
    }
    persistQueue();
    if (state.table) { await loadOrder(state.table.id); renderBill(); }
    await loadTables();
  } finally {
    flushInFlight = false;
    scheduleQueueRetry();
  }
}

// V1-TBL-010 follow-up (Semih, 2026-09-12: "başka iş yükü artışı yaptığımız
// ne varsa bul"): V1-WTR-017's "hafif gecikme kontrolü" used to sit here,
// intercepting BOTH send buttons with a forced "gönder birlikte / ayrı
// gönder" choice whenever a round's items had prep-time estimates ≥10
// minutes apart - a very common shape (any starter + main combination).
// Removed outright rather than tuned: V1-WTR-025's course system already
// solves the same "food shouldn't all arrive unevenly" problem, more
// deliberately (a waiter who cares assigns courses; FireRound/FireCourse
// stagger the actual kitchen dispatch) and without ever touching
// prepTimeMinutes - the two mechanisms were solving the same problem
// twice, and this one fired on every send regardless of whether the
// waiter had already handled staggering via courses. Send is a single
// action again, exactly like every other quick-add path.
export async function sendDraft() {
  if (state.draft.length === 0 || !state.table || state.sendInFlight) return;
  state.sendInFlight = true;
  el.btnSendFromMenu.disabled = true;
  el.btnSendFromBill.disabled = true;

  const targetItems = state.draft;
  const payload = draftToPayload(targetItems);
  const tableNumber = state.table.number;
  try {
    if (!state.isOnline) {
      queueOrder(payload);
      removeSentDraftLines(targetItems);
      afterDraftChange();
      toast(`Bağlantı yok — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
      return;
    }

    const result = await postOrder(payload);
    if (result.ok) {
      removeSentDraftLines(targetItems);
      await loadOrder(state.table.id);
      await loadTables();
      afterDraftChange();
      showScreen('tables');
      toast(`${tableNumber} siparişi mutfağa gönderildi.`);
    } else if (result.status === 429) {
      // V1-RMD-185: found by the 2026-09-12 five-agent independent Garson
      // audit — this used to fall into the generic 4xx branch below,
      // which is right for an actual validation rejection (something
      // about THIS round is wrong, fix it and resend) but wrong for a
      // rate limit (nothing about the round is wrong, the server is just
      // asking to wait). flushQueue() already treats 429 as retryable and
      // never destroys the round for it; the live-send path now queues
      // the same way the network-failure branch below does, instead of
      // leaving the waiter staring at a rate-limit message with no clear
      // next step.
      queueOrder(payload);
      removeSentDraftLines(targetItems);
      afterDraftChange();
      toast(`Sunucu şu an yoğun — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
    } else if (result.status >= 400 && result.status < 500) {
      // A rejected order is kept on screen so nothing typed is lost.
      toast(result.message, { warning: true });
    } else {
      queueOrder(payload);
      removeSentDraftLines(targetItems);
      afterDraftChange();
      toast(`Sunucuya ulaşılamadı — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
    }
  } finally {
    state.sendInFlight = false;
    afterDraftChange();
  }
}
