// V12-CWB-001: QR customer menu browsing. Deliberately no bundler/framework
// (matches the Cashier/WaiterPwa vanilla shells) — this is a small,
// single-purpose page a customer's phone loads from a cold QR scan, so a
// build step buys nothing a plain script tag does not already give.
"use strict";

const SESSION_HEADER_NAME = "X-Alkaros-Qr-Session";
const SESSION_STORAGE_KEY = "alkaros.qr.sessionToken";
const TABLE_TOKEN_STORAGE_KEY = "alkaros.qr.tableToken";
const GENERIC_ERROR_MESSAGE = "Menü şu anda yüklenemedi, lütfen daha sonra tekrar deneyin.";

// V12-CWB-002: the cart contract OrderEntry's own page (order-entry.js)
// reads and writes too — sessionStorage is the entire integration surface
// between the two pages, deliberately: no shared JS module, just one
// documented array shape (each line: productId, name, unitPrice, quantity,
// notes). Kept here since this is the page that first creates it.
const CART_STORAGE_KEY = "alkaros.qr.cart";

/** A cart line is the product alone, or the product with its chosen extras (same product, other extras = other line). */
function lineKeyFor(productId, modifierIds) {
  return modifierIds.length === 0 ? productId : `${productId}|${[...modifierIds].sort().join(",")}`;
}

function groupIsSatisfied(group, chosenIds) {
  const count = group.modifiers.filter((modifier) => chosenIds.has(modifier.modifierId)).length;
  return count >= group.minSelections && count <= group.maxSelections;
}

const CartStore = {
  read() {
    try {
      const raw = sessionStorage.getItem(CART_STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  },
  write(lines) {
    try {
      sessionStorage.setItem(CART_STORAGE_KEY, JSON.stringify(lines));
    } catch {
      // Private browsing / storage disabled — the cart just does not
      // survive a page reload; adding to it in the same page view still
      // works since CartStore.read() falls back to an empty array either way.
    }
  },
  addItem(product, quantity, modifiers = []) {
    const lines = CartStore.read();
    const lineKey = lineKeyFor(product.productId, modifiers.map((modifier) => modifier.modifierId));
    const existing = lines.find((line) => (line.lineKey || line.productId) === lineKey);
    if (existing) {
      existing.quantity += quantity;
    } else {
      lines.push({
        lineKey,
        productId: product.productId,
        name: product.name,
        unitPrice: product.unitPrice + modifiers.reduce((sum, modifier) => sum + modifier.priceDelta, 0),
        quantity,
        notes: null,
        modifiers: modifiers.map(({ modifierId, name, priceDelta }) => ({ modifierId, name, priceDelta })),
      });
    }
    CartStore.write(lines);
    return lines;
  },
};

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

/**
 * V1-SET-009: the business's own name/color/logo (V1-SET-007/008) — public,
 * session-free, deliberately independent of the menu/session flow below.
 * Cosmetic only: a failed fetch must never block the page's real job of
 * showing the menu, so this never throws to its caller.
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
      <span class="product-info">
        <span class="product-name"></span>
        <span class="product-category"></span>
      </span>
      <span class="product-price"></span>
      <button type="button" class="add-to-cart-button">Sepete ekle</button>
    `;
    item.querySelector(".product-name").textContent = product.name;
    item.querySelector(".product-category").textContent = product.categoryName;
    item.querySelector(".product-price").textContent = formatPrice(product.unitPrice);
    item.querySelector(".add-to-cart-button").addEventListener("click", () => {
      if ((product.modifierGroups || []).length > 0) {
        openModifierPicker(product);
        return;
      }
      CartStore.addItem(product, 1);
      updateCartBar();
    });
    list.appendChild(item);
  }
}

/** Asks the customer for the product's extras; "Sepete ekle" stays disabled until every group's minimum and maximum hold. */
function openModifierPicker(product) {
  const chosen = new Set();
  const overlay = document.createElement("div");
  overlay.className = "modifier-picker";
  overlay.setAttribute("role", "dialog");
  overlay.setAttribute("aria-modal", "true");
  overlay.setAttribute("aria-label", `${product.name} seçenekleri`);

  const title = document.createElement("h2");
  title.textContent = product.name;
  overlay.appendChild(title);

  const confirmButton = document.createElement("button");
  confirmButton.type = "button";
  confirmButton.className = "modifier-picker-confirm";
  confirmButton.textContent = "Sepete ekle";
  const refresh = () => {
    confirmButton.disabled = !product.modifierGroups.every((group) => groupIsSatisfied(group, chosen));
  };

  for (const group of product.modifierGroups) {
    const fieldset = document.createElement("fieldset");
    const legend = document.createElement("legend");
    legend.textContent = `${group.name}${group.minSelections > 0 ? " (zorunlu)" : " (isteğe bağlı)"}${group.maxSelections > 1 ? ` · en fazla ${group.maxSelections}` : ""}`;
    fieldset.appendChild(legend);
    for (const modifier of group.modifiers) {
      const label = document.createElement("label");
      const input = document.createElement("input");
      input.type = group.maxSelections === 1 ? "radio" : "checkbox";
      input.name = group.modifierGroupId;
      input.addEventListener("change", () => {
        if (group.maxSelections === 1) group.modifiers.forEach((other) => chosen.delete(other.modifierId));
        if (input.checked) chosen.add(modifier.modifierId);
        else chosen.delete(modifier.modifierId);
        refresh();
      });
      label.appendChild(input);
      label.appendChild(document.createTextNode(` ${modifier.name}${modifier.priceDelta > 0 ? ` +${formatPrice(modifier.priceDelta)}` : ""}`));
      fieldset.appendChild(label);
    }
    overlay.appendChild(fieldset);
  }

  const cancelButton = document.createElement("button");
  cancelButton.type = "button";
  cancelButton.className = "modifier-picker-cancel";
  cancelButton.textContent = "Vazgeç";
  cancelButton.addEventListener("click", () => overlay.remove());
  confirmButton.addEventListener("click", () => {
    const picked = product.modifierGroups.flatMap((group) => group.modifiers).filter((modifier) => chosen.has(modifier.modifierId));
    CartStore.addItem(product, 1, picked);
    updateCartBar();
    overlay.remove();
  });
  overlay.appendChild(cancelButton);
  overlay.appendChild(confirmButton);
  refresh();
  document.body.appendChild(overlay);
}

function updateCartBar() {
  const cartBar = document.getElementById("cartBar");
  const lines = CartStore.read();
  const itemCount = lines.reduce((sum, line) => sum + line.quantity, 0);

  if (itemCount === 0) {
    cartBar.hidden = true;
    return;
  }

  const total = lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
  document.getElementById("cartBarCount").textContent = `${itemCount} ürün`;
  document.getElementById("cartBarTotal").textContent = formatPrice(total);
  cartBar.hidden = false;
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
  void loadBranding();

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
    const menuItems = Array.isArray(page) ? page : page.items;
    const categories = groupByCategory(menuItems);
    let activeCategory = null;

    const renderActive = () => {
      const products = activeCategory === null
        ? menuItems
        : categories.get(activeCategory).products;
      renderCategories(categories, activeCategory, (code) => {
        activeCategory = code;
        renderActive();
      });
      renderProducts(products);
    };

    document.getElementById("loadingState").hidden = true;
    renderActive();
    updateCartBar();
  } catch (error) {
    showError(error instanceof Error ? error.message : GENERIC_ERROR_MESSAGE);
  }
}

if (typeof window !== "undefined" && typeof window.__ALKAROS_QR_MENU_SKIP_AUTOINIT__ === "undefined") {
  window.addEventListener("DOMContentLoaded", init);
}
