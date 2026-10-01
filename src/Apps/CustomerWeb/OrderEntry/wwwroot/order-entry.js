// V12-CWB-002: QR order entry — edits the cart Menu's own page (menu-app.js)
// created, submits it, then polls until the asynchronously-materialized
// Order (Order's own QrOrderSubmittedConsumer, V0-ARC-001 row 19) reaches a
// real status. No bundler/framework, same reasoning as menu-app.js.
"use strict";

const SESSION_HEADER_NAME = "X-Alkaros-Qr-Session";
// Shared contract with menu-app.js — same keys, deliberately duplicated
// here rather than factored into a shared module (see CartStore's own doc
// comment in menu-app.js): the whole point of the sessionStorage contract is
// that these two pages need nothing more than the documented key/shape to
// cooperate.
const SESSION_STORAGE_KEY = "alkaros.qr.sessionToken";
const TABLE_TOKEN_STORAGE_KEY = "alkaros.qr.tableToken";
const CART_STORAGE_KEY = "alkaros.qr.cart";
// V12-CWB-002's own duplicate-submission protection: generated once per
// checkout attempt and persisted, so reloading mid-submit (a dropped
// connection, an impatient reload) resends the identical id instead of
// creating a second queued submission — QrPendingOrderStore.SubmitAsync is
// idempotent on exactly this value.
const SUBMISSION_ID_STORAGE_KEY = "alkaros.qr.submissionId";
// Set only after the server has actually accepted the submission (the 202
// response arrived). The id above is persisted BEFORE the request is sent, so
// its mere presence cannot distinguish "server has this order" from "the
// request never got through" — resuming the poll on the latter would hide a
// still-unsent cart behind a poll for an order the server never saw.
const SUBMISSION_ACCEPTED_STORAGE_KEY = "alkaros.qr.submissionAccepted";
const GENERIC_ERROR_MESSAGE = "Siparişiniz şu anda gönderilemedi, lütfen daha sonra tekrar deneyin.";
const POLL_INTERVAL_MS = 2000;
const MAX_POLL_ATTEMPTS = 30;
// V1-RMD-391 (Tur 2, P2): the outbox delivery genuinely can take longer than
// 60s under real load, and the order keeps materializing server-side either
// way - stopping outright here used to leave the customer's screen frozen on
// "taking a bit long" forever, with no further updates and no hint that
// reloading would resume watching it. A slower cadence keeps this page
// checking, without hammering the server, for a further 20 minutes (a real
// guest is expected to have moved on well before that).
const SLOW_POLL_INTERVAL_MS = 10000;
const MAX_SLOW_POLL_ATTEMPTS = 120;

function readCart() {
  try {
    const raw = sessionStorage.getItem(CART_STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function writeCart(lines) {
  try {
    sessionStorage.setItem(CART_STORAGE_KEY, JSON.stringify(lines));
  } catch {
    // Private browsing / storage disabled — edits just do not persist
    // across a reload; this page view's own in-memory state still works.
  }
}

function clearCart() {
  try {
    sessionStorage.removeItem(CART_STORAGE_KEY);
  } catch {
    // Nothing to clean up if storage was never writable.
  }
}

function readStoredTableToken() {
  try {
    return sessionStorage.getItem(TABLE_TOKEN_STORAGE_KEY);
  } catch {
    return null;
  }
}

function readStoredSessionToken() {
  try {
    return sessionStorage.getItem(SESSION_STORAGE_KEY);
  } catch {
    return null;
  }
}

function storeSessionToken(token) {
  try {
    sessionStorage.setItem(SESSION_STORAGE_KEY, token);
  } catch {
    // Falls through to always re-issuing a session for this page load.
  }
}

/** Returns the persisted submission id only if the server already accepted it; null when none exists or the submit never went through. */
function readAcceptedSubmissionId() {
  try {
    if (sessionStorage.getItem(SUBMISSION_ACCEPTED_STORAGE_KEY) !== "1") return null;
    return sessionStorage.getItem(SUBMISSION_ID_STORAGE_KEY);
  } catch {
    return null;
  }
}

function markSubmissionAccepted() {
  try {
    sessionStorage.setItem(SUBMISSION_ACCEPTED_STORAGE_KEY, "1");
  } catch {
    // Without storage a reload simply shows the cart again, as before.
  }
}

function readOrCreateSubmissionId() {
  try {
    let id = sessionStorage.getItem(SUBMISSION_ID_STORAGE_KEY);
    if (!id) {
      id = crypto.randomUUID();
      sessionStorage.setItem(SUBMISSION_ID_STORAGE_KEY, id);
    }
    return id;
  } catch {
    // No persistence available — a reload mid-submit would resend as a new
    // id in this rare case, which the server still handles safely (a second
    // legitimate order), just not idempotently.
    return crypto.randomUUID();
  }
}

function clearSubmissionId() {
  try {
    sessionStorage.removeItem(SUBMISSION_ID_STORAGE_KEY);
    sessionStorage.removeItem(SUBMISSION_ACCEPTED_STORAGE_KEY);
  } catch {
    // Nothing to clean up if storage was never writable.
  }
}

async function parseErrorMessage(response) {
  try {
    const body = await response.json();
    if (body && body.error && typeof body.error.message === "string") {
      return body.error.message;
    }
  } catch {
    // Not a JSON error envelope — fall through to the generic message.
  }
  return GENERIC_ERROR_MESSAGE;
}

/** Exchanges the raw table token for a fresh customer session (mirrors menu-app.js's own issueSession). */
async function issueSession(tableToken) {
  const response = await fetch("/api/v1/qr/sessions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      tableToken,
      nonce: crypto.randomUUID(),
      timestamp: new Date().toISOString(),
    }),
  });

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }

  const body = await response.json();
  storeSessionToken(body.sessionToken);
  return body.sessionToken;
}

async function submitOrder(sessionToken, submissionId, cartLines) {
  const response = await fetch("/api/v1/qr/orders", {
    method: "POST",
    headers: { "Content-Type": "application/json", [SESSION_HEADER_NAME]: sessionToken },
    body: JSON.stringify({
      submissionId,
      items: cartLines.map((line) => ({
        id: crypto.randomUUID(),
        productId: line.productId,
        quantity: line.quantity,
        specialInstructions: line.notes || null,
        modifiers: (line.modifiers || []).map((modifier) => ({ modifierId: modifier.modifierId })),
      })),
    }),
  });

  if (response.status === 401) return null;
  if (!response.ok) throw new Error(await parseErrorMessage(response));
  return response.json();
}

/** Same transparent-retry-once shape as menu-app.js's loadMenuWithSessionRetry. */
async function submitOrderWithSessionRetry(tableToken, submissionId, cartLines) {
  let sessionToken = readStoredSessionToken();
  if (sessionToken) {
    const result = await submitOrder(sessionToken, submissionId, cartLines);
    if (result) return result;
  }

  sessionToken = await issueSession(tableToken);
  const result = await submitOrder(sessionToken, submissionId, cartLines);
  if (result) return result;

  throw new Error(GENERIC_ERROR_MESSAGE);
}

async function pollOrderOnce(submissionId) {
  const sessionToken = readStoredSessionToken();
  const response = await fetch(`/api/v1/qr/orders/${submissionId}`, {
    headers: { [SESSION_HEADER_NAME]: sessionToken },
  });
  if (!response.ok) throw new Error(await parseErrorMessage(response));
  return response.json();
}

/**
 * V0-ARC-001's own walk (Order's QrOrderSubmittedConsumer) passes through
 * Draft and Submitted on its way to PendingConfirmation, usually within one
 * outbox poll interval - a real, observed transient state, not a hung
 * request. Polling must keep going through those, not just "Pending" (the
 * server's own placeholder for "no orders.orders row yet at all").
 */
function isStillMaterializing(status) {
  return status === "Pending" || status === "Draft" || status === "Submitted";
}

function statusMessage(status) {
  switch (status) {
    case "PendingConfirmation":
      return "Siparişiniz alındı, personel onayı bekleniyor.";
    case "Accepted":
      return "Siparişiniz onaylandı ve mutfağa iletildi.";
    case "Rejected":
      return "Siparişiniz kabul edilmedi, lütfen garsonu çağırın.";
    default:
      return "Siparişiniz işleniyor…";
  }
}

async function pollUntilMaterialized(submissionId) {
  const statusSection = document.getElementById("orderStatus");
  const statusMessageEl = document.getElementById("orderStatusMessage");
  statusSection.hidden = false;

  for (let attempt = 0; attempt < MAX_POLL_ATTEMPTS; attempt++) {
    const outcome = await pollOrderOnce(submissionId);
    if (!isStillMaterializing(outcome.status)) {
      statusMessageEl.textContent = statusMessage(outcome.status);
      clearCart();
      clearSubmissionId();
      return;
    }
    statusMessageEl.textContent = "Siparişiniz mutfağa iletiliyor…";
    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
  }

  // Still Pending after MAX_POLL_ATTEMPTS - the outbox delivery is just slow,
  // not failed; the customer's own submission already succeeded (202) and
  // is not resubmitted. V1-RMD-391: keep watching at a slower cadence rather
  // than freezing this message forever.
  for (let attempt = 0; attempt < MAX_SLOW_POLL_ATTEMPTS; attempt++) {
    statusMessageEl.textContent = "Siparişiniz alındı, mutfağa iletilmesi biraz uzun sürüyor. Güncellemeler gelmeye devam ediyor…";
    await new Promise((resolve) => setTimeout(resolve, SLOW_POLL_INTERVAL_MS));
    const outcome = await pollOrderOnce(submissionId);
    if (!isStillMaterializing(outcome.status)) {
      statusMessageEl.textContent = statusMessage(outcome.status);
      clearCart();
      clearSubmissionId();
      return;
    }
  }

  // Gave up watching after a further 20 minutes - the submission is still
  // safe (idempotent, already accepted), but this page will not learn its
  // outcome on its own anymore. Tell the customer what actually helps,
  // instead of leaving them on a message that quietly stopped being true.
  statusMessageEl.textContent = "Siparişinizin işlenmesi beklenenden uzun sürüyor. Sayfayı yenileyin veya garsonu çağırın.";
}

function formatPrice(amount) {
  return new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(amount);
}

function renderCart(lines, onChange) {
  const list = document.getElementById("cartList");
  const emptyState = document.getElementById("emptyCartState");
  const summary = document.getElementById("cartSummary");
  const submitButton = document.getElementById("btnSubmitOrder");
  list.innerHTML = "";

  if (lines.length === 0) {
    list.hidden = true;
    summary.hidden = true;
    submitButton.hidden = true;
    emptyState.hidden = false;
    return;
  }

  emptyState.hidden = true;
  list.hidden = false;
  summary.hidden = false;
  submitButton.hidden = false;

  for (const line of lines) {
    const item = document.createElement("li");
    item.className = "cart-line";
    item.innerHTML = `
      <div class="cart-line-top">
        <span class="cart-line-name"></span>
        <span class="cart-line-total"></span>
      </div>
      <div class="cart-line-controls">
        <button type="button" class="qty-button" data-action="decrease">−</button>
        <span class="qty-value"></span>
        <button type="button" class="qty-button" data-action="increase">+</button>
        <button type="button" class="remove-line-button">Kaldır</button>
      </div>
      <input type="text" class="notes-input" placeholder="Not ekleyin (opsiyonel)" maxlength="200" />
    `;
    const lineKey = line.lineKey || line.productId;
    const extras = (line.modifiers || []).map((modifier) => modifier.name).join(", ");
    item.querySelector(".cart-line-name").textContent = extras ? `${line.name} (${extras})` : line.name;
    item.querySelector(".cart-line-total").textContent = formatPrice(line.unitPrice * line.quantity);
    item.querySelector(".qty-value").textContent = String(line.quantity);
    item.querySelector('[data-action="decrease"]').addEventListener("click", () => onChange(lineKey, -1));
    item.querySelector('[data-action="increase"]').addEventListener("click", () => onChange(lineKey, 1));
    item.querySelector(".remove-line-button").addEventListener("click", () => onChange(lineKey, -Infinity));
    const notesInput = item.querySelector(".notes-input");
    notesInput.value = line.notes || "";
    notesInput.addEventListener("change", () => {
      const current = readCart();
      const target = current.find((candidate) => (candidate.lineKey || candidate.productId) === lineKey);
      if (target) {
        target.notes = notesInput.value.trim() || null;
        writeCart(current);
      }
    });
    list.appendChild(item);
  }

  const grandTotal = lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
  document.getElementById("cartGrandTotal").textContent = formatPrice(grandTotal);
}

function showError(message) {
  const errorState = document.getElementById("errorState");
  errorState.textContent = message;
  errorState.hidden = false;
}

/**
 * V1-SET-009: the business's own name/color/logo (V1-SET-007/008) — public,
 * session-free, deliberately independent of the cart/submit flow below.
 * Cosmetic only: a failed fetch must never block checkout, so this never
 * throws to its caller (same contract as menu-app.js's own loadBranding()).
 */
async function loadBranding() {
  try {
    const response = await fetch("/api/v1/qr/branding");
    if (!response.ok) return;
    const branding = await response.json();

    document.documentElement.style.setProperty("--cw-accent", branding.accentColor);

    const brandBlock = document.getElementById("businessBrand");
    let hasBrand = false;
    if (branding.businessName) {
      const nameEl = document.getElementById("businessName");
      nameEl.textContent = branding.businessName;
      nameEl.hidden = false;
      hasBrand = true;
    }
    if (branding.hasLogo) {
      const logoEl = document.getElementById("businessLogo");
      logoEl.src = "/api/v1/qr/logo";
      logoEl.hidden = false;
      hasBrand = true;
    }
    brandBlock.hidden = !hasBrand;
  } catch {
    // Network error / malformed response — the page keeps ALKAROS's own
    // default look, exactly as if branding had never been configured.
  }
}

function init() {
  void loadBranding();

  // A same-tab reload (dropped connection, an impatient reload) while a
  // submission is already in flight leaves both the cart and the submission
  // id in sessionStorage, since neither is cleared until pollUntilMaterialized
  // reaches a terminal status. Resume watching that existing order instead of
  // showing the cart form again — re-showing it would invite a confusing
  // resubmit with a possibly-edited cart, whose extra/changed lines the
  // server's submissionId-keyed idempotency would silently ignore anyway
  // (the original order is what gets returned, never a second one).
  const existingSubmissionId = readAcceptedSubmissionId();
  if (existingSubmissionId) {
    document.getElementById("cartList").hidden = true;
    document.getElementById("cartSummary").hidden = true;
    document.getElementById("btnSubmitOrder").hidden = true;
    document.getElementById("emptyCartState").hidden = true;
    void pollUntilMaterialized(existingSubmissionId);
    return;
  }

  let lines = readCart();

  const onChange = (lineKey, delta) => {
    const target = lines.find((line) => (line.lineKey || line.productId) === lineKey);
    if (!target) return;
    target.quantity += delta;
    lines = lines.filter((line) => line.quantity > 0);
    writeCart(lines);
    renderCart(lines, onChange);
  };

  renderCart(lines, onChange);

  const submitButton = document.getElementById("btnSubmitOrder");
  // Double-click / slow-request guard (same shape as Cashier's own
  // dispatchInFlight pattern) — the server-side idempotency on submissionId
  // is the real guarantee; this just avoids firing the request twice.
  let submissionInFlight = false;
  submitButton.addEventListener("click", async () => {
    if (submissionInFlight) return;
    const currentCart = readCart();
    if (currentCart.length === 0) return;

    const tableToken = readStoredTableToken();
    if (!tableToken) {
      showError("Oturum bilgisi bulunamadı, lütfen QR kodunu tekrar okutun.");
      return;
    }

    submissionInFlight = true;
    submitButton.disabled = true;
    submitButton.textContent = "Gönderiliyor…";

    try {
      const submissionId = readOrCreateSubmissionId();
      const result = await submitOrderWithSessionRetry(tableToken, submissionId, currentCart);
      markSubmissionAccepted();
      submitButton.hidden = true;
      await pollUntilMaterialized(result.submissionId);
    } catch (error) {
      showError(error instanceof Error ? error.message : GENERIC_ERROR_MESSAGE);
      submitButton.disabled = false;
      submitButton.textContent = "Siparişi Gönder";
    } finally {
      submissionInFlight = false;
    }
  });
}

if (typeof window !== "undefined" && typeof window.__ALKAROS_QR_ORDER_ENTRY_SKIP_AUTOINIT__ === "undefined") {
  window.addEventListener("DOMContentLoaded", init);
}
