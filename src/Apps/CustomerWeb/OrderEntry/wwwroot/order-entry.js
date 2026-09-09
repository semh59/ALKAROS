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
const GENERIC_ERROR_MESSAGE = "Siparişiniz şu anda gönderilemedi, lütfen daha sonra tekrar deneyin.";
const POLL_INTERVAL_MS = 2000;
const MAX_POLL_ATTEMPTS = 30;

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
  // is not resubmitted.
  statusMessageEl.textContent = "Siparişiniz alındı, mutfağa iletilmesi biraz uzun sürüyor.";
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
      <input type="text" class="notes-input" placeholder="Not ekleyin (opsiyonel)" />
    `;
    item.querySelector(".cart-line-name").textContent = line.name;
    item.querySelector(".cart-line-total").textContent = formatPrice(line.unitPrice * line.quantity);
    item.querySelector(".qty-value").textContent = String(line.quantity);
    item.querySelector('[data-action="decrease"]').addEventListener("click", () => onChange(line.productId, -1));
    item.querySelector('[data-action="increase"]').addEventListener("click", () => onChange(line.productId, 1));
    item.querySelector(".remove-line-button").addEventListener("click", () => onChange(line.productId, -Infinity));
    const notesInput = item.querySelector(".notes-input");
    notesInput.value = line.notes || "";
    notesInput.addEventListener("change", () => {
      const current = readCart();
      const target = current.find((candidate) => candidate.productId === line.productId);
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

function init() {
  let lines = readCart();

  const onChange = (productId, delta) => {
    const target = lines.find((line) => line.productId === productId);
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
