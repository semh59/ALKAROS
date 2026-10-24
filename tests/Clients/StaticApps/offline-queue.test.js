// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const modulePath = resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/js/offline-queue.js");

document.body.innerHTML = '<div id="toasts"></div><div id="ribbon"></div><span id="ribbonText"></span><button id="ribbonQueue"></button><div id="pendingBanner" hidden></div><div id="tablesGrid"></div><div id="zoneChips"></div><span id="pendingTitle"></span><span id="pendingSub"></span>';

const { sortQueueByPriority, isQueuedFor, flushQueue, isTransientOrderStatus, isTransientReconcileFailure, reconcileOfflineActions, MAX_RECONCILE_BATCH } = await import(modulePath);
const { state, adoptDeviceOwner, PersistentKeyMap } = await import(resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/js/state.js"));

function round(id, { courseNumbers = [], queuedAt } = {}) {
  return {
    id,
    queuedAt,
    items: courseNumbers.map((courseNumber) => ({ courseNumber })),
  };
}

// A fixed "now" close to every fixture's queuedAt keeps these tests from
// depending on the wall clock (sortQueueByPriority defaults `now` to
// Date.now()) and from ever tripping the aging boost below.
const NOW = new Date("2026-09-15T10:00:10.000Z").getTime();

describe("sortQueueByPriority", () => {
  it("sends a starter round before a dessert round queued earlier", () => {
    const dessert = round("dessert", { courseNumbers: [3], queuedAt: "2026-09-15T10:00:00.000Z" });
    const starter = round("starter", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });

    const sorted = sortQueueByPriority([dessert, starter], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["starter", "dessert"]);
  });

  it("uses the round's lowest course number when a round mixes courses", () => {
    const mixed = round("mixed", { courseNumbers: [2, 1], queuedAt: "2026-09-15T10:00:00.000Z" });
    const main = round("main-only", { courseNumbers: [2], queuedAt: "2026-09-15T10:00:01.000Z" });

    const sorted = sortQueueByPriority([main, mixed], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["mixed", "main-only"]);
  });

  it("breaks a tie within the same course by queue time, oldest first", () => {
    const later = round("later", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });
    const earlier = round("earlier", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:00.000Z" });

    const sorted = sortQueueByPriority([later, earlier], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["earlier", "later"]);
  });

  it("treats a round with no course-tagged items as course 0, not last", () => {
    const noCourse = round("no-course", { courseNumbers: [], queuedAt: "2026-09-15T10:00:00.000Z" });
    const main = round("main", { courseNumbers: [2], queuedAt: "2026-09-15T09:59:00.000Z" });

    const sorted = sortQueueByPriority([main, noCourse], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["no-course", "main"]);
  });

  it("does not mutate the original queue array", () => {
    const queue = [
      round("b", { courseNumbers: [2], queuedAt: "2026-09-15T10:00:00.000Z" }),
      round("a", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:00.000Z" }),
    ];
    const originalOrder = queue.map((r) => r.id);

    sortQueueByPriority(queue, NOW);

    expect(queue.map((r) => r.id)).toEqual(originalOrder);
  });

  it("boosts a round that has aged past the starvation threshold ahead of a fresher, lower-course round", () => {
    // "dessert" was queued 3 minutes before "now" (past the 2-minute aging
    // threshold) and would otherwise keep losing to every newly-queued
    // starter round forever, per the bug an independent audit found: a
    // low-priority round re-sorted to the back on every flushQueue() call,
    // with no aging term, can be perpetually re-buried by newer arrivals
    // whenever something ahead of it keeps hitting a transient failure.
    const dessert = round("dessert", { courseNumbers: [3], queuedAt: "2026-09-15T09:57:00.000Z" });
    const freshStarter = round("fresh-starter", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });

    const sorted = sortQueueByPriority([freshStarter, dessert], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["dessert", "fresh-starter"]);
  });

  it("orders two aged rounds by queuedAt, oldest first", () => {
    const olderAged = round("older-aged", { courseNumbers: [3], queuedAt: "2026-09-15T09:56:00.000Z" });
    const newerAged = round("newer-aged", { courseNumbers: [1], queuedAt: "2026-09-15T09:57:30.000Z" });

    const sorted = sortQueueByPriority([newerAged, olderAged], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["older-aged", "newer-aged"]);
  });
});

describe("what the device keeps when the server does not take an answer", () => {
  it("keeps a round on a session, lock, timeout, row-version race or rate-limit answer, and drops it on a real rejection", () => {
    for (const status of [401, 403, 408, 423, 429]) expect(isTransientOrderStatus(status)).toBe(true);
    expect(isTransientOrderStatus(409, "CONCURRENCY_CONFLICT")).toBe(true);
    for (const code of ["INSUFFICIENT_STOCK", "PRODUCT_STOCK_NOT_CONFIGURED", "TABLE_CHECK_ALREADY_OPEN", "TABLE_LIMIT_REACHED", "DUPLICATE_RESOURCE", "IDEMPOTENCY_KEY_REUSED", undefined]) {
      expect(isTransientOrderStatus(409, code)).toBe(false);
    }
    for (const status of [400, 404, 422]) expect(isTransientOrderStatus(status)).toBe(false);
  });

  async function flushWith(answers) {
    state.terminalId = "t1";
    state.isOnline = true;
    state.user = { userId: "w1" };
    state.failedOrders = [];
    state.offlineQueue = answers.map((answer, index) => ({ id: `round-${index}`, ownerUserId: "w1", tableNumber: index + 1, items: [], queuedAt: `2026-09-15T10:00:0${index}.000Z`, answer }));
    const sent = [];
    vi.stubGlobal("fetch", vi.fn((url, config) => {
      if (!config || !config.body || String(url).includes("submit-draft")) return Promise.resolve(new Response("{}", { status: 200 }));
      const body = JSON.parse(config.body);
      sent.push(body.id);
      const answer = answers[Number(body.id.split("-")[1])];
      return Promise.resolve(new Response(JSON.stringify(answer.body ?? {}), { status: answer.status }));
    }));
    try {
      await flushQueue();
    } finally {
      vi.unstubAllGlobals();
    }
    const result = { sent, queued: state.offlineQueue.map((round) => round.id), failed: state.failedOrders.map((round) => round.id) };
    state.offlineQueue = [];
    state.failedOrders = [];
    state.user = null;
    return result;
  }

  it("moves a round the server will never take to the failed list and still sends the rounds behind it", async () => {
    const stock = { status: 409, body: { error: { code: "INSUFFICIENT_STOCK", message: "Stok yetersiz." } } };
    const result = await flushWith([stock, { status: 200 }]);
    expect(result).toEqual({ sent: ["round-0", "round-1"], queued: [], failed: ["round-0"] });
  });

  it("keeps a round that lost a row-version race and stops, so the order of rounds is kept", async () => {
    const race = { status: 409, body: { error: { code: "CONCURRENCY_CONFLICT", message: "Çakışma." } } };
    const result = await flushWith([race, { status: 200 }]);
    expect(result).toEqual({ sent: ["round-0"], queued: ["round-0", "round-1"], failed: [] });
  });

  it("treats a network failure, an expired session, a busy server and any 5xx as retryable for offline actions", () => {
    expect(isTransientReconcileFailure({ offline: true, status: 0 })).toBe(true);
    for (const status of [401, 408, 423, 429, 500, 502, 503]) expect(isTransientReconcileFailure({ status })).toBe(true);
    for (const status of [400, 403, 404, 409]) expect(isTransientReconcileFailure({ status })).toBe(false);
  });
});

describe("reconcileOfflineActions", () => {
  const actions = (count) => Array.from({ length: count }, (_, index) => ({ idempotencyKey: `k-${index}`, permissionCode: "bills.comp" }));
  const answer = (status, body) => new Response(JSON.stringify(body), { status });

  function setUp(count) {
    state.terminalId = "t1";
    state.offlineBudget = { budgetId: "b1", roleCode: "waiter", userId: "u1", lines: [], used: {} };
    state.offlineActions = actions(count);
  }

  afterEach(() => { vi.unstubAllGlobals(); state.offlineActions = []; state.offlineBudget = null; });

  it("keeps every queued action when the server fails or is unreachable", async () => {
    for (const reply of [() => answer(503, {}), () => answer(500, {}), () => Promise.reject(new TypeError("Failed to fetch"))]) {
      setUp(2);
      vi.stubGlobal("fetch", vi.fn(reply));
      await reconcileOfflineActions();
      expect(state.offlineActions).toHaveLength(2);
    }
  });

  it("drops the actions the server refused for good and shows its Turkish reason", async () => {
    setUp(2);
    vi.stubGlobal("fetch", vi.fn(() => answer(404, { error: { code: "UNKNOWN_BUDGET", message: "Çevrimdışı yetki bütçesi bulunamadı; yeniden bağlanın." } })));
    await reconcileOfflineActions();
    expect(state.offlineActions).toHaveLength(0);
    expect(document.getElementById("toasts").textContent).toContain("Çevrimdışı yetki bütçesi bulunamadı");
  });

  it("sends a long queue in batches no larger than the server accepts", async () => {
    setUp(MAX_RECONCILE_BATCH + 5);
    const sizes = [];
    vi.stubGlobal("fetch", vi.fn((_url, config) => {
      sizes.push(JSON.parse(config.body).actions.length);
      return Promise.resolve(answer(200, { summary: { brief: "tamam" }, results: [] }));
    }));
    await reconcileOfflineActions();
    expect(sizes).toEqual([MAX_RECONCILE_BATCH, 5]);
    expect(state.offlineActions).toHaveLength(0);
  });
});

describe("a device that changes hands", () => {
  afterEach(() => { localStorage.clear(); });

  it("forgets the previous waiter's drafts, offline actions, rejected notes and request keys but keeps unsent rounds", () => {
    const keys = new PersistentKeyMap("alkaros_waiter_void_keys");
    adoptDeviceOwner("waiter-1");
    state.draftsByTable.set("t1", { number: 1, lines: [{ id: "x" }] });
    state.offlineActions = [{ idempotencyKey: "a" }];
    state.failedOrders = [{ id: "f" }];
    state.offlineQueue = [{ id: "round-1" }];
    keys.set("item-1", "key-1");

    adoptDeviceOwner("waiter-1");
    expect(state.offlineActions).toHaveLength(1);
    expect(keys.get("item-1")).toBe("key-1");

    adoptDeviceOwner("waiter-2");
    expect(state.draftsByTable.size).toBe(0);
    expect(state.offlineActions).toHaveLength(0);
    expect(state.failedOrders).toHaveLength(0);
    expect(keys.size).toBe(0);
    expect(new PersistentKeyMap("alkaros_waiter_void_keys").size).toBe(0);
    expect(state.offlineQueue).toHaveLength(1);
  });

  it("keeps a request key across a reload", () => {
    new PersistentKeyMap("alkaros_waiter_comp_keys").set("item-9", "key-9");
    expect(new PersistentKeyMap("alkaros_waiter_comp_keys").get("item-9")).toBe("key-9");
  });
});

describe("rounds stay with the waiter who queued them", () => {
  it("is a round for its owner, for an untagged old round and when nobody is known, never for another waiter", () => {
    expect(isQueuedFor({ ownerUserId: "w1" }, "w1")).toBe(true);
    expect(isQueuedFor({}, "w2")).toBe(true);
    expect(isQueuedFor({ ownerUserId: "w1" }, undefined)).toBe(true);
    expect(isQueuedFor({ ownerUserId: "w1" }, "w2")).toBe(false);
  });

  it("sends only the signed-in waiter's rounds and leaves the other waiter's round queued", async () => {
    state.terminalId = "t1";
    state.isOnline = true;
    state.user = { userId: "w2" };
    state.offlineQueue = [
      { id: "round-w1", ownerUserId: "w1", items: [], queuedAt: "2026-09-15T10:00:00.000Z" },
      { id: "round-w2", ownerUserId: "w2", items: [], queuedAt: "2026-09-15T10:00:01.000Z" },
    ];
    const sent = [];
    vi.stubGlobal("fetch", vi.fn((url, config) => {
      sent.push(JSON.parse(config.body));
      return Promise.resolve(new Response(JSON.stringify({}), { status: 200 }));
    }));
    try {
      await flushQueue();
    } finally {
      vi.unstubAllGlobals();
    }
    expect(state.offlineQueue.map((round) => round.id)).toEqual(["round-w1"]);
    expect(JSON.stringify(sent)).not.toContain("round-w1");
    state.offlineQueue = [];
    state.user = null;
  });
});
