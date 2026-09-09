// V12-CWB-001: QR customer menu browsing. Deliberately no bundler/framework
// (matches the Cashier/WaiterPwa vanilla shells) — this is a small,
// single-purpose page a customer's phone loads from a cold QR scan, so a
// build step buys nothing a plain script tag does not already give.
"use strict";

const SESSION_HEADER_NAME = "X-Alkaros-Qr-Session";
const SESSION_STORAGE_KEY = "alkaros.qr.sessionToken";
const TABLE_TOKEN_STORAGE_KEY = "alkaros.qr.tableToken";
const GENERIC_ERROR_MESSAGE = "Menü şu anda yüklenemedi, lütfen daha sonra tekrar deneyin.";

function getTableTokenFromUrl() {
  const params = new URLSearchParams(window.location.search);
  return params.get("t");
}

/** The raw table token never needs to survive a full page unload for this
 * page's own scope (no cart to preserve), but keeping it lets a same-tab
 * reload re-authenticate without the query string surviving verbatim. */
function rememberTableToken(token) {
  try {
    sessionStorage.setItem(TABLE_TOKEN_STORAGE_KEY, token);
  } catch {
    // Private browsing / storage disabled — the query string is still the
    // source of truth for this same page load, so this is not fatal.
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
    // Not a JSON error envelope — fall through to the generic message.
  }
  return GENERIC_ERROR_MESSAGE;
}

/** Exchanges the raw table token for a customer session (V12-QRS-003). */
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

async function fetchMenu(sessionToken) {
  const response = await fetch("/api/v1/qr/menu", {
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

/**
 * Fetches the menu for a stored or freshly-issued session, transparently
 * re-issuing exactly once on a stale session (401 — expired/idle/revoked,
 * V12-QRS-003's own three reasons) before giving up. A customer whose table
 * token itself is no longer valid (revoked/expired) sees that failure
 * surface here instead, with the backend's own Turkish message.
 */
async function loadMenuWithSessionRetry(tableToken) {
  let sessionToken = readStoredSessionToken();
  if (sessionToken) {
    const page = await fetchMenu(sessionToken);
    if (page) return page;
  }

  sessionToken = await issueSession(tableToken);
  const page = await fetchMenu(sessionToken);
  if (page) return page;

  throw new Error(GENERIC_ERROR_MESSAGE);
}

function formatPrice(amount) {
  return new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(amount);
}

function groupByCategory(products) {
  const categories = new Map();
  for (const product of products) {
    if (!categories.has(product.categoryCode)) {
      categories.set(product.categoryCode, { name: product.categoryName, products: [] });
    }
    categories.get(product.categoryCode).products.push(product);
  }
  return categories;
}

function renderCategories(categories, activeCode, onSelect) {
  const nav = document.getElementById("categoryTabs");
  nav.innerHTML = "";

  const allTab = createCategoryTab("Tümü", activeCode === null, () => onSelect(null));
  nav.appendChild(allTab);

  for (const [code, { name }] of categories) {
    nav.appendChild(createCategoryTab(name, activeCode === code, () => onSelect(code)));
  }
}

function createCategoryTab(label, isActive, onClick) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "category-tab";
  button.textContent = label;
  button.setAttribute("aria-pressed", String(isActive));
  button.addEventListener("click", onClick);
  return button;
}

function renderProducts(products) {
  const list = document.getElementById("productList");
  const emptyState = document.getElementById("emptyState");
  list.innerHTML = "";

  if (products.length === 0) {
    list.hidden = true;
    emptyState.hidden = false;
    return;
  }

  emptyState.hidden = true;
  list.hidden = false;
  for (const product of products) {
    const item = document.createElement("li");
    item.className = "product-card";
    item.innerHTML = `
      <span>
        <span class="product-name"></span>
        <span class="product-category"></span>
      </span>
      <span class="product-price"></span>
    `;
    item.querySelector(".product-name").textContent = product.name;
    item.querySelector(".product-category").textContent = product.categoryName;
    item.querySelector(".product-price").textContent = formatPrice(product.unitPrice);
    list.appendChild(item);
  }
}

function showError(message) {
  document.getElementById("loadingState").hidden = true;
  document.getElementById("productList").hidden = true;
  document.getElementById("emptyState").hidden = true;
  const errorState = document.getElementById("errorState");
  errorState.textContent = message;
  errorState.hidden = false;
}

async function init() {
  const urlToken = getTableTokenFromUrl();
  const tableToken = urlToken || readStoredTableToken();
  if (!tableToken) {
    showError("QR kodu okunamadı, lütfen masadaki kodu tekrar okutun.");
    return;
  }
  if (urlToken) {
    rememberTableToken(urlToken);
  }

  try {
    const page = await loadMenuWithSessionRetry(tableToken);
    const categories = groupByCategory(page.items);
    let activeCategory = null;

    const renderActive = () => {
      const products = activeCategory === null
        ? page.items
        : categories.get(activeCategory).products;
      renderCategories(categories, activeCategory, (code) => {
        activeCategory = code;
        renderActive();
      });
      renderProducts(products);
    };

    document.getElementById("loadingState").hidden = true;
    renderActive();
  } catch (error) {
    showError(error instanceof Error ? error.message : GENERIC_ERROR_MESSAGE);
  }
}

if (typeof window !== "undefined" && typeof window.__ALKAROS_QR_MENU_SKIP_AUTOINIT__ === "undefined") {
  window.addEventListener("DOMContentLoaded", init);
}
