import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = (app) => resolve(here, "..", "..", "..", "src/Clients", app, "wwwroot");
const cashierScript = readFileSync(resolve(wwwroot("Cashier"), "telemetry/telemetry.js"), "utf8");
const waiterScript = readFileSync(resolve(wwwroot("WaiterPwa"), "telemetry/telemetry.js"), "utf8");

const INTERACTIONS = "/api/v1/telemetry/interactions";
const ERRORS = "/api/v1/telemetry/browser-errors";
const OWN_KEY = "alkaros.telemetry.interactions.v1";

function memoryStorage({ quota = Infinity } = {}) {
  const data = new Map();
  return {
    data,
    getItem: (key) => (data.has(key) ? data.get(key) : null),
    removeItem: (key) => data.delete(key),
    setItem(key, value) {
      const total = [...data].filter(([k]) => k !== key).reduce((sum, [, v]) => sum + v.length, 0) + String(value).length;
      if (total > quota) throw new DOMException("quota", "QuotaExceededError");
      data.set(key, String(value));
    },
  };
}

function boot(options = {}) {
  window.__ALKAROS_TELEMETRY_MANUAL = true;
  window.eval(cashierScript);
  const sent = [];
  const storage = options.storage ?? memoryStorage();
  const handle = window.AlkarosTelemetry.install({
    send: (url, body) => (sent.push({ url, body: JSON.parse(body) }), true),
    storage,
    afterPaint: (callback) => callback(),
    ...options,
  });
  return { handle, sent, storage };
}

const interactionKeys = (sent) => sent.filter((s) => s.url === INTERACTIONS).flatMap((s) => s.body.items.map((i) => i.key));

describe("telemetry script copies", () => {
  it("is byte-identical in the cashier and waiter apps", () => {
    expect(waiterScript).toBe(cashierScript);
  });

  it.each([
    ["Cashier", "index.html", "./telemetry/telemetry.js"],
    ["Cashier", "payments/split-payment/index.html", "../../telemetry/telemetry.js"],
    ["Cashier", "payments/cash-session/index.html", "../../telemetry/telemetry.js"],
    ["Cashier", "payments/customer-accounts/index.html", "../../telemetry/telemetry.js"],
    ["WaiterPwa", "index.html", "./telemetry/telemetry.js"],
  ])("%s %s loads the script", (app, page, src) => {
    expect(readFileSync(resolve(wwwroot(app), page), "utf8")).toContain(`<script src="${src}"></script>`);
  });

  it("is precached by the waiter service worker", () => {
    expect(readFileSync(resolve(wwwroot("WaiterPwa"), "sw.js"), "utf8")).toContain("'./telemetry/telemetry.js'");
  });
});

describe("telemetry reporting", () => {
  let session;
  beforeEach(() => {
    document.body.innerHTML = `
      <button id="approve-confirmation">Onayla</button>
      <button id="submit-tender">Tahsil</button>
      <button id="apply-split">Böl</button>
      <button id="print-receipt">Fiş</button>
      <button data-refund-action="request" data-payment-id="p1">İade</button>
      <button class="x">Anahtarsız</button>`;
  });
  afterEach(() => session?.handle.uninstall());

  it("records the five payment screen buttons under fixed keys", () => {
    session = boot();
    for (const button of document.querySelectorAll("button:not(.x)")) button.click();
    session.handle.flush();

    expect(interactionKeys(session.sent)).toEqual([
      "id:approve-confirmation", "id:submit-tender", "id:apply-split", "id:print-receipt", "refund-action:request",
    ]);
  });

  it("falls back to a route level key and never sends visible text", () => {
    session = boot();
    document.querySelector("button.x").click();
    session.handle.flush();

    expect(interactionKeys(session.sent)).toEqual(["auto:button@/"]);
    expect(JSON.stringify(session.sent)).not.toContain("Anahtarsız");
  });

  it("prefers data-telemetry-id over the element id", () => {
    document.body.innerHTML = '<button id="a" data-telemetry-id="b.x.y.1">t</button>';
    session = boot();
    document.querySelector("button").click();
    session.handle.flush();

    expect(interactionKeys(session.sent)).toEqual(["b.x.y.1"]);
  });

  it("reports an uncaught error at most three times per location", () => {
    session = boot();
    for (let i = 0; i < 5; i += 1) {
      window.dispatchEvent(new ErrorEvent("error", { message: "secret text", filename: "a.js", lineno: 1, colno: 2 }));
    }

    const reported = session.sent.filter((s) => s.url === ERRORS);
    expect(reported).toHaveLength(3);
    expect(reported[0].body.kind).toBe("js_error");
    expect(JSON.stringify(reported)).not.toContain("secret text");
  });

  it("reports a failed api call as a network error without the query string", async () => {
    const original = window.fetch;
    window.fetch = () => Promise.reject(new TypeError("offline"));
    try {
      session = boot();
      await window.fetch("/api/v1/orders/123?x=secret").catch(() => {});
      await Promise.resolve();

      const reported = session.sent.filter((s) => s.url === ERRORS);
      expect(reported).toHaveLength(1);
      expect(reported[0].body.kind).toBe("network_error");
      expect(JSON.stringify(reported)).not.toContain("secret");
    } finally {
      session?.handle.uninstall();
      window.fetch = original;
    }
  });

  it("never lets the waiter offline queue hit quota because of its own queue", () => {
    const orders = JSON.stringify([{ id: "o1", lines: "x".repeat(2000) }]);
    const storage = memoryStorage({ quota: orders.length + 21000 });
    storage.setItem("alkaros_waiter_offline_queue", orders);
    session = boot({ storage });

    for (let i = 0; i < 400; i += 1) document.querySelector("#submit-tender").click();

    expect(storage.data.get(OWN_KEY).length).toBeLessThanOrEqual(20000);
    expect(() => storage.setItem("alkaros_waiter_offline_queue", orders)).not.toThrow();
    expect(storage.data.get("alkaros_waiter_offline_queue")).toBe(orders);
  });

  it("drops only its own queue when storage rejects the write", () => {
    const orders = "o".repeat(1000);
    const storage = memoryStorage({ quota: 1050 });
    storage.setItem("alkaros_waiter_offline_queue", orders);
    session = boot({ storage });

    document.querySelector("#submit-tender").click();

    expect(session.handle.size()).toBe(0);
    expect(storage.data.get("alkaros_waiter_offline_queue")).toBe(orders);
  });

  it("keeps items queued when the send fails and resends them later", () => {
    let accept = false;
    const sent = [];
    session = boot({ send: (url, body) => (accept ? (sent.push(JSON.parse(body)), true) : false) });
    document.querySelector("#submit-tender").click();

    expect(session.handle.flush()).toBe(false);
    expect(session.handle.size()).toBe(1);
    accept = true;
    expect(session.handle.flush()).toBe(true);
    expect(sent[0].items[0].key).toBe("id:submit-tender");
  });
});
