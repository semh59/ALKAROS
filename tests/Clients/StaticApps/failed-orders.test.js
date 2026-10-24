// @vitest-environment jsdom

import { beforeEach, describe, expect, it } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/WaiterPwa/wwwroot/js");

document.body.innerHTML = `<div id="toasts"></div><div id="ribbon"></div><span id="ribbonText"></span><button id="ribbonQueue"></button>
<div id="pendingBanner" hidden></div><div id="tablesGrid"></div><div id="zoneChips"></div><span id="pendingTitle"></span><span id="pendingSub"></span>
<div id="optionsBackdrop"></div><div id="optionsSheet"></div><span id="optionsTitle"></span><span id="optionsSub"></span><div id="optionsBody"></div>
<button id="optionsClose"></button><button id="optionsConfirm"></button><span id="optionsFootLabel"></span><span id="optionsFootValue"></span>`;

const { state, el } = await import(resolve(wwwroot, "state.js"));
const { openFailedOrdersSheet, dismissFailedOrder, retryFailedOrder } = await import(resolve(wwwroot, "sheets/failed-orders.js"));
const { openPendingSheet, resolvePendingGuarded } = await import(resolve(wwwroot, "sheets/pending-orders.js"));

beforeEach(() => {
  state.user = { userId: "w1" };
  state.offlineQueue = [];
  state.failedOrders = [{ id: "f1", tableNumber: 4, items: [{ name: "Çay", quantity: 2 }], rejectedAt: "2026-09-15T10:00:00.000Z", error: "Stok yetersiz." }];
});

describe("failed rounds", () => {
  it("asks before deleting: the first tap arms the row, the second tap deletes it", () => {
    openFailedOrdersSheet();
    dismissFailedOrder("f1");
    expect(state.failedOrders).toHaveLength(1);
    expect(el.optionsBody.textContent).toContain("Silinsin mi? Evet");
    dismissFailedOrder("f1");
    expect(state.failedOrders).toHaveLength(0);
  });

  it("puts a failed round back in the queue under the same id without its rejection note", () => {
    openFailedOrdersSheet();
    retryFailedOrder("f1");
    expect(state.failedOrders).toHaveLength(0);
    expect(state.offlineQueue).toHaveLength(1);
    expect(state.offlineQueue[0]).toMatchObject({ id: "f1", tableNumber: 4, ownerUserId: "w1" });
    expect(state.offlineQueue[0].error).toBeUndefined();
    expect(state.offlineQueue[0].rejectedAt).toBeUndefined();
  });
});

describe("guest order rejection", () => {
  it("rejects only on the second tap of the same order", async () => {
    state.pending = [{ orderId: "o1", tableNumber: 2, total: 10, itemCount: 1, createdAt: "2026-09-15T10:00:00.000Z" }];
    openPendingSheet();
    const button = el.optionsBody.querySelector("[data-reject]");
    await resolvePendingGuarded(button, "o1", false);
    expect(el.optionsBody.textContent).toContain("Reddedilsin mi? Evet");
  });
});
