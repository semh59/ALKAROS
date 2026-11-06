import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const wwwroot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "src/Clients/Cashier/wwwroot");
const TERMINAL = "11111111-1111-1111-1111-111111111111";

const CATALOG = [
  { productId: "00000000-0000-0000-0000-000000000001", name: "Kola", unitPrice: 45, categoryCode: "drinks", categoryName: "İçecek", sku: "K1", remainingCount: 4 },
  { productId: "00000000-0000-0000-0000-000000000002", name: "İskender", unitPrice: 180, categoryCode: "food", categoryName: "Yemek", sku: "I1", remainingCount: null },
  { productId: "00000000-0000-0000-0000-000000000003", name: "Su", unitPrice: 10, categoryCode: "drinks", categoryName: "İçecek", sku: "S1", remainingCount: 5 },
];

function routes({ catalogStatus = 200, displayName = "Ayşe Kasiyer" } = {}) {
  return [
    { test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1", displayName } },
    { test: (url) => url.endsWith("/catalog"), status: catalogStatus, body: catalogStatus === 200 ? CATALOG : {} },
    { test: (url) => url.endsWith("/orders/staff"), body: [] },
    { test: (url) => url.endsWith("/orders/suggested-waiter"), status: 404 },
    { test: (url) => url.endsWith("/orders/waiter-load"), body: [] },
  ];
}

async function openTill(options) {
  installFetchRouter(routes(options));
  await loadApp(resolve(wwwroot, "index.html"), resolve(wwwroot, "cashier-app.js"));
  await vi.waitFor(() => {
    if (!document.querySelector(".pos-product-card") && !document.querySelector(".pos-catalog-state--error")) throw new Error("catalog not settled yet");
  });
}

const cardNames = () => [...document.querySelectorAll(".pos-product-name")].map((node) => node.textContent);
const card = (name) => [...document.querySelectorAll(".pos-product-card")].find((node) => node.querySelector(".pos-product-name").textContent === name);
const ticketRows = () => document.querySelectorAll(".ticket-row").length;

describe("cashier catalog", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows the unit price and category the server sends under their real field names", async () => {
    await openTill();

    expect(card("İskender").querySelector(".pos-product-price").textContent.replace(/\s/g, " ")).toMatch(/180,00/);
    const tabs = [...document.querySelectorAll(".tab-chip")].map((node) => node.textContent.trim());
    expect(tabs).toEqual(expect.arrayContaining(["İçecek", "Yemek"]));

    [...document.querySelectorAll(".tab-chip")].find((node) => node.textContent.trim() === "Yemek").click();
    expect(cardNames()).toEqual(["İskender"]);
  });

  it("marks a product with fewer than five units left and shows nothing for an untracked one", async () => {
    await openTill();

    expect(card("Kola").querySelector(".product-stock").classList.contains("is-low")).toBe(true);
    expect(card("Su").querySelector(".product-stock").classList.contains("is-low")).toBe(false);
    expect(card("İskender").querySelector(".product-stock")).toBeNull();
  });

  it("says nothing matches instead of showing a blank grid", async () => {
    await openTill();
    const input = document.getElementById("searchInput");

    input.value = "zzz";
    input.dispatchEvent(new Event("input", { bubbles: true }));

    await vi.waitFor(() => expect(document.getElementById("productMatrix").textContent).toContain("Aramanıza uyan ürün yok."));
  });

  it("keeps ordering closed and invents no product when the catalog cannot be read", async () => {
    await openTill({ catalogStatus: 500 });

    expect(document.querySelectorAll(".pos-product-card")).toHaveLength(0);
    expect(document.getElementById("productMatrix").textContent).toContain("Katalog sunucudan alınamadı");
    expect(document.getElementById("btnDispatchOrder").disabled).toBe(true);
  });

  it("offers no complimentary (ikram) action on the cashier order screen; that goes through the approved item flow", async () => {
    await openTill();
    card("Kola").click();

    expect(document.getElementById("ticketItemsStream").textContent.toLowerCase()).not.toContain("ikram");
    const rowActions = [...document.querySelectorAll(".ticket-row [data-action]")].map((node) => node.dataset.action);
    expect(rowActions.length).toBeGreaterThan(0);
    expect(new Set(rowActions)).toEqual(new Set(["dec", "inc", "del"].filter((action) => rowActions.includes(action))));
  });

  it("names the signed-in cashier from the session, with no fixed name", async () => {
    await openTill({ displayName: "Mehmet Kasiyer" });

    expect(document.getElementById("cashierNameLabel").textContent).toBe("Mehmet Kasiyer");
    expect(document.body.textContent).not.toContain("Zeynep");
  });
});

describe("cashier parked tickets", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  const confirmModal = () => document.getElementById("confirmModal");

  async function parkOneCola() {
    await openTill();
    card("Kola").click();
    document.getElementById("btnParkTicket").click();
    expect(ticketRows()).toBe(0);
    expect(document.getElementById("parkCountBadge").textContent).toBe("1");
  }

  it("brings a parked ticket back into an empty cart without asking", async () => {
    await parkOneCola();

    document.getElementById("btnRecallTicket").click();
    document.querySelector('[data-action="recall"]').click();

    await vi.waitFor(() => expect(ticketRows()).toBe(1));
    expect(confirmModal().hidden).toBe(true);
    expect(document.getElementById("parkCountBadge").style.display).toBe("none");
  });

  it("asks before replacing a filled cart, keeps the cart when declined and replaces it when accepted", async () => {
    await parkOneCola();
    card("İskender").click();
    document.getElementById("btnRecallTicket").click();

    document.querySelector('[data-action="recall"]').click();
    await vi.waitFor(() => expect(confirmModal().hidden).toBe(false));
    expect(document.getElementById("confirmModalMessage").textContent).toContain("mevcut sepeti değiştirecektir");

    document.getElementById("btnConfirmModalCancel").click();
    await vi.waitFor(() => expect(confirmModal().hidden).toBe(true));
    expect(document.getElementById("ticketItemsStream").textContent).toContain("İskender");
    expect(document.getElementById("parkCountBadge").textContent).toBe("1");

    document.querySelector('[data-action="recall"]').click();
    await vi.waitFor(() => expect(confirmModal().hidden).toBe(false));
    document.getElementById("btnConfirmModalAccept").click();

    await vi.waitFor(() => expect(document.getElementById("ticketItemsStream").textContent).toContain("Kola"));
    expect(document.getElementById("ticketItemsStream").textContent).not.toContain("İskender");
  });
});

describe("cashier static contract", () => {
  const html = readFileSync(resolve(wwwroot, "index.html"), "utf8");

  it("ships a standalone installable manifest", () => {
    const manifest = JSON.parse(readFileSync(resolve(wwwroot, "manifest.json"), "utf8"));

    expect(manifest).toMatchObject({ display: "standalone", start_url: "./index.html" });
    for (const key of ["name", "short_name", "theme_color", "background_color"]) expect(manifest[key]).toBeTruthy();
  });

  it("has the ordering controls and no payment claim on the order screen", () => {
    for (const id of ["searchInput", "categoryTabs", "productMatrix", "ticketItemsStream", "grandTotalAmount", "btnDispatchOrder", "btnParkTicket", "btnRecallTicket"]) {
      expect(html).toContain(`id="${id}"`);
    }
    expect(html).not.toContain("Tahsilat Başarılı");
    expect(html).not.toContain("Nakit Tahsilat & Fiş Kes");
    expect(html).not.toContain("changeModal");
  });

  it("styles the remaining-count badge and its low state", () => {
    const css = readFileSync(resolve(wwwroot, "cashier-app.css"), "utf8");

    expect(css).toContain(".product-stock");
    expect(css).toMatch(/\.product-stock\.is-low/);
  });
});
