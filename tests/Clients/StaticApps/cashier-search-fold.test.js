import { beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const wwwroot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "src/Clients/Cashier/wwwroot");
const names = ["Isparta Gülü", "İstanbul Simit", "Iğdır Kebap", "Çorba"];

async function openTill() {
  installFetchRouter([
    { test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: "11111111-1111-1111-1111-111111111111", userId: "u1", displayName: "Test Kasiyer" } },
    { test: (url) => url.endsWith("/catalog"), body: names.map((name, index) => ({ productId: `00000000-0000-0000-0000-00000000000${index + 1}`, name, unitPrice: 10, categoryCode: "c1", categoryName: "Genel", sku: index === 0 ? "ISPARTA-1" : index === 3 ? "KIZ-9" : `S${index}` })) },
    { test: (url) => url.endsWith("/orders/staff"), body: [] },
    { test: (url) => url.endsWith("/orders/suggested-waiter"), status: 404 },
    { test: (url) => url.endsWith("/orders/waiter-load"), body: [] },
  ]);
  await loadApp(resolve(wwwroot, "index.html"), resolve(wwwroot, "cashier-app.js"));
  await vi.waitFor(() => expect(document.querySelector(".pos-product-card")).not.toBeNull());
}

async function search(query) {
  const input = document.getElementById("searchInput");
  input.value = query;
  input.dispatchEvent(new Event("input", { bubbles: true }));
  const shown = () => [...document.querySelectorAll(".pos-product-name")].map((node) => node.textContent);
  await vi.waitFor(() => expect(shown().length).toBeLessThan(names.length), { timeout: 1000 });
  return shown();
}

describe("cashier-app.js product search folds the Turkish dotted and dotless i", () => {
  beforeEach(() => { vi.resetModules(); localStorage.clear(); });

  it.each([
    ["isparta", ["Isparta Gülü"]],
    ["ISPARTA", ["Isparta Gülü"]],
    ["ıspar", ["Isparta Gülü"]],
    ["istanbul", ["İstanbul Simit"]],
    ["iğdır", ["Iğdır Kebap"]],
    ["İSTANBUL", ["İstanbul Simit"]],
    ["kiz", ["Çorba"]],
  ])("finds %s", async (query, expected) => {
    await openTill();
    expect(await search(query)).toEqual(expected);
  });

  it("adds the product a scanned code names when the code is typed with a dotless i", async () => {
    await openTill();
    const input = document.getElementById("searchInput");
    input.value = "ısparta-1";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    await vi.waitFor(() => expect(input.value).toBe(""), { timeout: 1000 });
  });
});
