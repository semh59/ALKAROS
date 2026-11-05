// @vitest-environment jsdom

import { beforeEach, describe, expect, it } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/WaiterPwa/wwwroot/js");

document.body.innerHTML = `<div id="productList"></div><div id="cartBar"></div><span id="cartCount"></span><span id="cartTotal"></span>
<div id="categoryChips"></div>`;

const { state, el } = await import(resolve(wwwroot, "state.js"));
const { renderProducts, addToDraft } = await import(resolve(wwwroot, "screens/menu.js"));
const { productSheetHtml } = await import(resolve(wwwroot, "sheets/product-sheet.js"));

// The screens write into many elements this test does not look at; an element the page above lacks is a detached one.
for (const key of Object.keys(el)) if (!el[key]) el[key] = document.createElement("div");

const product = (id, allowsHalfPortion) => ({ id, name: `Ürün ${id}`, price: 10, categoryCode: "c", modifierGroups: [], ...(allowsHalfPortion === undefined ? {} : { allowsHalfPortion }) });

beforeEach(() => {
  state.products = [product("half", true), product("whole", false), product("unknown")];
  state.search = "";
  state.activeCategory = "all";
  state.draft = [];
});

describe("waiter half portions", () => {
  it("shows the half button only on a product the manager flagged", () => {
    renderProducts();

    expect(el.productList.querySelector('[data-half="half"]')).not.toBeNull();
    expect(el.productList.querySelector('[data-half="half"]').getAttribute("aria-label")).toBe("Ürün half — yarım porsiyon ve miktar seç");
    expect(el.productList.querySelector('[data-half="whole"]')).toBeNull();
    expect(el.productList.querySelector('[data-half="unknown"]')).toBeNull();
  });

  it("offers 0,5 in the product sheet only for a flagged product and whole quantities otherwise", () => {
    const quantities = (item) => {
      state.optionsContext = { product: item, quantity: 1, chosen: new Map(), seatId: null, courseNumber: null };
      return [...productSheetHtml().matchAll(/data-qty="([\d.]+)"/g)].map((match) => match[1]);
    };

    expect(quantities(product("half", true))).toEqual(["0.5", "1", "1.5", "2", "3"]);
    expect(quantities(product("whole", false))).toEqual(["1", "2", "3", "4", "5"]);
    expect(quantities(product("unknown"))).toEqual(["1", "2", "3", "4", "5"]);
  });

  it("remembers the flag on the draft line so the stepper knows its size", () => {
    addToDraft(product("half", true), 1, [], "", null, null);
    addToDraft(product("whole", false), 1, [], "", null, null);

    expect(state.draft.map((line) => line.allowsHalfPortion)).toEqual([true, false]);
  });
});
