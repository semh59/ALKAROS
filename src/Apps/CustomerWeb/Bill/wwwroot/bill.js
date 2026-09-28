// V1-WTR-018: garson-karsilastirma idea #6 - "misafir için salt-okunur canlı
// adisyon". Read-only: this page never calls /orders, only polls /bill.
// Same no-bundler shape and sessionStorage contract as menu-app.js/
// order-entry.js (see those files' own comments on why it is deliberately
// duplicated instead of a shared module).
"use strict";

const SESSION_HEADER_NAME = "X-Alkaros-Qr-Session";
const SESSION_STORAGE_KEY = "alkaros.qr.sessionToken";
const TABLE_TOKEN_STORAGE_KEY = "alkaros.qr.tableToken";
const GENERIC_ERROR_MESSAGE = "Adisyon şu anda yüklenemedi, lütfen daha sonra tekrar deneyin.";
const POLL_INTERVAL_MS = 5000;

function getTableTokenFromUrl() {
  const params = new URLSearchParams(window.location.search);
  return params.get("t");
}

function rememberTableToken(token) {
  try {
    sessionStorage.setItem(TABLE_TOKEN_STORAGE_KEY, token);
  } catch {
    // Private browsing / storage disabled - the query string (if present)
    // is still the source of truth for this same page load.
  }
}

function readStoredTableToken() {
  try {
    return sessionStorage.getItem(TABLE_TOKEN_STORAGE_KEY);
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

function readStoredSessionToken() {
  try {
    return sessionStorage.getItem(SESSION_STORAGE_KEY);
  } catch {
    return null;
  }
}

async function parseErrorMessage(response) {
  try {
    const body = await response.json();
    if (body && body.error && typeof body.error.message === "string") {
      return body.error.message;
    }
  } catch {
    // Not a JSON error envelope - fall through to the generic message.
  }
  return GENERIC_ERROR_MESSAGE;
}

/** Exchanges the raw table token for a customer session (mirrors menu-app.js's own issueSession). */
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

async function fetchBill(sessionToken) {
  const response = await fetch("/api/v1/qr/bill", {
    headers: { [SESSION_HEADER_NAME]: sessionToken },
  });

  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw new Error(await parseErrorMessage(response));
  }

  return response.json();
}

/** Same transparent-retry-once shape as menu-app.js's loadMenuWithSessionRetry. */
async function fetchBillWithSessionRetry(tableToken) {
  let sessionToken = readStoredSessionToken();
  if (sessionToken) {
    const bill = await fetchBill(sessionToken);
    if (bill) return bill;
  }

  sessionToken = await issueSession(tableToken);
  const bill = await fetchBill(sessionToken);
  if (bill) return bill;

  throw new Error(GENERIC_ERROR_MESSAGE);
}

function formatPrice(amount) {
  return new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(amount);
}

function renderBill(bill) {
  const loadingState = document.getElementById("loadingState");
  const emptyState = document.getElementById("emptyBillState");
  const list = document.getElementById("billList");
  const summary = document.getElementById("billSummary");
  loadingState.hidden = true;
  // V1-RMD-392 (Tur 2, P2): a successful render always means whatever
  // earlier failure put up the error banner (the very first poll, if that
  // one failed) no longer applies - showError() itself never clears it, and
  // nothing else ever did either, so a customer whose FIRST poll failed but
  // whose second one succeeded used to keep seeing a permanent error banner
  // sitting right above their own correctly-updating bill.
  document.getElementById("errorState").hidden = true;

  if (!bill.hasActiveOrder || bill.lines.length === 0) {
    list.hidden = true;
    summary.hidden = true;
    emptyState.hidden = false;
    return;
  }

  emptyState.hidden = true;
  list.hidden = false;
  summary.hidden = false;

  list.innerHTML = "";
  for (const line of bill.lines) {
    const item = document.createElement("li");
    item.className = "bill-line";
    item.innerHTML = `
      <div>
        <div class="bill-line-name"></div>
        <div class="bill-line-qty"></div>
      </div>
      <span class="bill-line-total"></span>
    `;
    item.querySelector(".bill-line-name").textContent = line.name;
    item.querySelector(".bill-line-qty").textContent = `${line.quantity} × ${formatPrice(line.unitPrice)}`;
    item.querySelector(".bill-line-total").textContent = formatPrice(line.lineTotal);
    list.appendChild(item);
  }

  document.getElementById("billSubtotal").textContent = formatPrice(bill.subtotal);
  document.getElementById("billTax").textContent = formatPrice(bill.taxTotal);
  document.getElementById("billTotal").textContent = formatPrice(bill.total);
}

function showError(message) {
  document.getElementById("loadingState").hidden = true;
  const errorState = document.getElementById("errorState");
  errorState.textContent = message;
  errorState.hidden = false;
}

/**
 * V1-SET-009: the business's own name/color/logo (V1-SET-007/008) — public,
 * session-free, deliberately independent of the bill polling below.
 * Cosmetic only: a failed fetch must never block this page's real job of
 * showing the live bill (same contract as menu-app.js's own loadBranding()).
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

// V1-RMD-392 (Tur 2, P2): true once any poll has ever rendered a real bill.
// A later transient failure (the one the header's own "canlı güncellenir"
// promise and this file's startPolling() comment both already assume is
// routine) must not bury an already-showing, still-correct bill under a
// scary permanent error banner - it just keeps the last good total on
// screen and quietly retries. Only a failure BEFORE the first success ever
// shows the error state, since there is nothing else to show yet.
let hasRenderedOnce = false;

async function pollOnce(tableToken) {
  try {
    const bill = await fetchBillWithSessionRetry(tableToken);
    hasRenderedOnce = true;
    renderBill(bill);
  } catch (error) {
    if (!hasRenderedOnce) {
      showError(error instanceof Error ? error.message : GENERIC_ERROR_MESSAGE);
    }
  }
}

function startPolling(tableToken) {
  void pollOnce(tableToken);
  // A dropped/erroring poll does not stop the next one - a guest's own
  // network blip must not permanently freeze this page on a stale total.
  window.setInterval(() => void pollOnce(tableToken), POLL_INTERVAL_MS);
}

function init() {
  void loadBranding();

  const urlToken = getTableTokenFromUrl();
  if (urlToken) rememberTableToken(urlToken);
  const tableToken = urlToken || readStoredTableToken();

  if (!tableToken) {
    showError("Oturum bilgisi bulunamadı, lütfen QR kodunu tekrar okutun.");
    return;
  }

  startPolling(tableToken);
}

if (typeof window !== "undefined" && typeof window.__ALKAROS_QR_BILL_SKIP_AUTOINIT__ === "undefined") {
  window.addEventListener("DOMContentLoaded", init);
}
