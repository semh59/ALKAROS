// @vitest-environment jsdom

import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/WaiterPwa/wwwroot");

document.body.innerHTML = `<div id="productList"></div><div id="cartBar"></div><span id="cartCount"></span><span id="cartTotal"></span>
<div id="categoryChips"></div>`;
const { state } = await import(resolve(wwwroot, "js/state.js"));
const { renderProducts } = await import(resolve(wwwroot, "js/screens/menu.js"));

function ownFiles(directory) {
  return readdirSync(directory).flatMap((name) => {
    if (name === "vendor" || name === "node_modules") return [];
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return ownFiles(path);
    return /\.(html|css|js|json)$/.test(name) && !name.endsWith(".test.js") ? [path] : [];
  });
}

describe("the waiter app makes no request to an outside address", () => {
  it("has no http(s) address other than an XML namespace in its own pages, styles and scripts", () => {
    const offenders = ownFiles(wwwroot).flatMap((path) =>
      [...readFileSync(path, "utf8").matchAll(/https?:\/\/[^\s"'`)<>]+/g)]
        .map((match) => match[0])
        .filter((address) => !address.startsWith("http://www.w3.org/"))
        .map((address) => `${path.slice(wwwroot.length)}: ${address}`));

    expect(offenders).toEqual([]);
  });

  it("renews the offline cache so an installed app drops the page that still named the font service", () => {
    const version = Number(/alkaros-waiter-v([0-9]+)/.exec(readFileSync(resolve(wwwroot, "sw.js"), "utf8"))?.[1]);
    expect(version).toBeGreaterThanOrEqual(22);
  });

  it("still names a system font stack so the text shows without a downloaded font", () => {
    expect(readFileSync(resolve(wwwroot, "waiter-app.css"), "utf8")).toMatch(/--font-sans:[^;]*system-ui/);
  });
});

describe("the product search folds the Turkish dotted and dotless i", () => {
  const text = (query) => {
    state.search = query;
    state.activeCategory = "all";
    state.catalogFailed = false;
    state.draft = [];
    state.products = ["Isparta Gülü", "İstanbul Simit", "Iğdır Kebap", "Çorba"].map((name, index) => ({ id: String(index), name, categoryCode: "c", price: 10, modifierGroups: [] }));
    renderProducts();
    return document.getElementById("productList").textContent; };

  it.each([
    ["isparta", "Isparta Gülü"],
    ["ISPARTA", "Isparta Gülü"],
    ["ıspar", "Isparta Gülü"],
    ["istanbul", "İstanbul Simit"],
    ["İSTANBUL", "İstanbul Simit"],
    ["iğdır", "Iğdır Kebap"],
    ["corba", null],
  ])("finds %s", (query, expected) => {
    const shown = text(query);
    if (expected) expect(shown).toContain(expected);
    else expect(shown).toContain("Aramanıza uyan ürün yok.");
  });

  it("keeps products out that do not match", () => {
    const shown = text("isparta");
    expect(shown).not.toContain("İstanbul Simit");
    expect(shown).not.toContain("Çorba");
  });
});
