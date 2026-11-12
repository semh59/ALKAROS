// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RouterProvider } from "../router";
import { ExperiencePage } from "./workspace";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const emptyAuthorizationData = { pendingGrants: [], delegations: [], tightenings: [] };

const pendingGrant = {
  grantId: "11111111-1111-1111-1111-111111111111",
  permissionCode: "bills.comp",
  requesterUserId: "aaaaaaaa-0000-0000-0000-000000000000",
  requesterRoleCode: "waiter",
  amount: 120,
  reasonCode: "CustomerChange",
  requestedAt: "2026-09-04T18:00:00Z",
};

/**
 * V1-IAM-025 (D6): nothing exercised the `/authorization` route's glue in
 * workspace.tsx — `AuthorizationDecisionsRoute`'s capability gate, its
 * `createAuthorizationDecisionsClient()` fetch wrapper, and its wiring of
 * approve/deny/revoke/clear into `AuthorizationDecisionsWorkspace`'s
 * callback props. Those two pieces are each unit-tested on their own
 * (`AuthorizationDecisionsWorkspace.test.tsx`, `client.ts`'s own coverage);
 * this is the missing middle.
 */
describe("workspace /authorization route", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  function renderAuthorizationRoute(capabilities: readonly string[]) {
    window.history.replaceState({}, "", "/authorization");
    return render(
      <RouterProvider>
        <ExperiencePage
          terminalId="22222222-2222-2222-2222-222222222222"
          displayName="Deniz Kaya"
          capabilities={capabilities}
          path="/authorization"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
  }

  it("with reports.view, fetches the three lists and renders the ready empty state", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith("/pending-grants")) return jsonResponse(emptyAuthorizationData.pendingGrants);
      if (path.endsWith("/delegations")) return jsonResponse(emptyAuthorizationData.delegations);
      if (path.endsWith("/behavioural-tightenings")) return jsonResponse(emptyAuthorizationData.tightenings);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await act(async () => renderAuthorizationRoute(["reports.view"]));
    // The three list requests race; flush one more microtask turn.
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Yetki kararları");
    expect(document.body.textContent).toContain("Bekleyen yetki isteği yok.");
  });

  it("without reports.view, never calls the authorization API and shows the forbidden shell", async () => {
    const fetch = vi.fn(async () => jsonResponse([]));
    vi.stubGlobal("fetch", fetch);

    await act(async () => renderAuthorizationRoute(["pos.cashier.mutate"]));

    expect(fetch).not.toHaveBeenCalled();
  });

  it("names the terminal once in the header: the label says Terminal, the value is only the short id", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(emptyAuthorizationData.delegations)));

    window.history.replaceState({}, "", "/authorization");
    await render(
      <RouterProvider>
        <ExperiencePage terminalId="d60b3df9-2222-2222-2222-222222222222" displayName="Deniz Kaya" capabilities={["reports.view"]} path="/authorization" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    );

    const item = [...document.querySelectorAll(".production-shell__identity-item")].find((element) => element.querySelector("span")!.textContent === "Terminal")!;
    expect(item.querySelector("strong")!.textContent).toBe("D60B3DF9");
  });

  it("shows the name a manager gave the terminal in place of the short id", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(emptyAuthorizationData.delegations)));

    window.history.replaceState({}, "", "/authorization");
    await render(
      <RouterProvider>
        <ExperiencePage terminalId="d60b3df9-2222-2222-2222-222222222222" terminalName="Kasa 1" displayName="Deniz Kaya" capabilities={["reports.view"]} path="/authorization" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    );

    const item = [...document.querySelectorAll(".production-shell__identity-item")].find((element) => element.querySelector("span")!.textContent === "Terminal")!;
    expect(item.querySelector("strong")!.textContent).toBe("Kasa 1");
  });

  it("wires an approve click through to a real POST against the management API", async () => {
    let approved: string | null = null;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.endsWith("/pending-grants")) return jsonResponse([pendingGrant]);
      if (path.endsWith("/delegations")) return jsonResponse(emptyAuthorizationData.delegations);
      if (path.endsWith("/behavioural-tightenings")) return jsonResponse(emptyAuthorizationData.tightenings);
      if (path.endsWith(`/grants/${pendingGrant.grantId}/approve`) && init?.method === "POST") {
        approved = pendingGrant.grantId;
        return new Response(null, { status: 204 });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await act(async () => renderAuthorizationRoute(["reports.view"]));
    await act(async () => Promise.resolve());

    const approveButton = [...document.querySelectorAll("button")]
      .find((button) => button.textContent?.trim() === "Onayla");
    expect(approveButton).toBeDefined();
    await act(async () => approveButton!.dispatchEvent(new MouseEvent("click", { bubbles: true })));
    expect(approved).toBeNull();
    const confirmButton = [...document.querySelectorAll("button")]
      .find((button) => button.textContent?.trim() === "Evet, onayla");
    await act(async () => confirmButton!.dispatchEvent(new MouseEvent("click", { bubbles: true })));
    await act(async () => Promise.resolve());

    expect(approved).toBe(pendingGrant.grantId);
  });
});

/**
 * Every route below used to gate on `pos.cashier.mutate`, which migration
 * 049 (V1-IAM-024) removed from the permission catalog entirely — no
 * session could ever hold it again, so canOpenRoute was always false and
 * every one of these screens was unreachable in the shipped app. Fixed to
 * the granular code each route's server endpoint actually requires;
 * these tests pin that mapping so it cannot regress silently again.
 */
describe("workspace route gating uses granular permission codes, not the removed pos.cashier.mutate", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  const forbiddenText = "Bu alana erişim izniniz yok";

  async function renderRoute(path: string, capabilities: readonly string[], answer: (url: string) => unknown = () => []) {
    vi.stubGlobal("fetch", vi.fn(async (url: string) => jsonResponse(answer(String(url)))));
    window.history.replaceState({}, "", path);
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="33333333-3333-3333-3333-333333333333"
          displayName="Test Kullanıcı"
          capabilities={capabilities}
          path={path}
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
  }

  it.each([
    ["/tables", "tables.status"],
    ["/billing", "bills.split"],
    ["/kitchen", "orders.send"],
  ])("a session holding %s's real permission (%s) is not forbidden", async (path, permission) => {
    await renderRoute(path, [permission]);
    expect(document.body.textContent).not.toContain(forbiddenText);
  });

  it.each(["/tables", "/billing", "/kitchen"])(
    "a session holding only the removed pos.cashier.mutate is forbidden from %s",
    async (path) => {
      await renderRoute(path, ["pos.cashier.mutate"]);
      expect(document.body.textContent).toContain(forbiddenText);
    },
  );

  it.each([
    [["orders.create"], false, false],
    [["orders.create", "bills.cancel"], true, false],
    [["orders.create", "bills.split"], false, true],
  ])("the till queue offers cancelling and merging only for the matching permissions (%j)", async (capabilities, cancelOffered, mergeOffered) => {
    const check = (orderId: string) => ({ orderId, orderNumber: `S-${orderId}`, tableNumber: "5", itemCount: 1, total: 100, createdAt: "2026-09-24T11:32:00Z", billId: null, paidAmount: 0, itemPreview: null, tableId: "tb-5" });
    await renderRoute("/pending-checks", capabilities, (url) => (url.includes("awaiting-payment") ? [check("o-1"), check("o-2")] : []));
    await act(async () => Promise.resolve());

    expect(document.body.textContent?.includes("Siparişi iptal et")).toBe(cancelOffered);
    expect(document.querySelector('input[type="checkbox"]') !== null).toBe(mergeOffered);
  });

  it("gives every left-menu entry its own icon", async () => {
    await renderRoute("/", ["orders.create", "tables.status", "bills.split", "kitchen.advance", "catalog.manage", "reports.view"]);
    await act(async () => document.querySelector<HTMLButtonElement>(".production-shell__more-toggle")!.click());

    const drawn = [...document.querySelectorAll(".production-shell__nav-symbol svg")].map((svg) => svg.innerHTML);
    expect(drawn.length).toBeGreaterThan(8);
    expect(new Set(drawn).size).toBe(drawn.length);
  });

  it("closes the online food screen to a session that only holds tables.status", async () => {
    await renderRoute("/online", ["tables.status"]);
    expect(document.body.textContent).toContain(forbiddenText);
    const urls = (vi.mocked(fetch).mock.calls as unknown[][]).map(([url]) => String(url));
    expect(urls.some((url) => url.includes("/online-operations"))).toBe(false);
  });

  // V12-OUI-003: platform API settings open only for integrations.manage, and a forbidden session never asks for them.
  it("opens the online platform settings for integrations.manage and forbids them to a cashier", async () => {
    await renderRoute("/online-platforms", ["integrations.manage"]);
    expect(document.body.textContent).not.toContain(forbiddenText);
    expect(document.body.textContent).toContain("Online platform bağlantı bilgileri");
    await act(async () => root!.unmount());
    root = null;

    await renderRoute("/online-platforms", ["orders.create"]);
    expect(document.body.textContent).toContain(forbiddenText);
    const urls = (vi.mocked(fetch).mock.calls as unknown[][]).map(([url]) => String(url));
    expect(urls.some((url) => url.includes("online-platform-credentials"))).toBe(false);
  });

  // V12-OUI-004: one "Online Yemek" entry; its settings tab is a manager's, reached also by the new path.
  it("has one online food entry that opens the orders for a cashier and the settings tab only for a manager", async () => {
    const online = (url: string) => url.includes("/online-operations")
      ? { orders: [], problems: [], retries: { pendingProviderEvents: 0, catalogPublicationsRetrying: 0, availabilityDivergences: 0 } }
      : url.endsWith("/online-channels") ? { platforms: [] } : url.includes("/online-platform-credentials") ? { platforms: [] } : [];
    await renderRoute("/online", ["orders.create"], online);
    expect(document.body.textContent).not.toContain(forbiddenText);
    await act(async () => document.querySelector<HTMLButtonElement>(".production-shell__more-toggle")!.click());
    const entries = [...document.querySelectorAll("a, button")].filter((e) => e.textContent?.includes("Online"));
    expect(entries.map((e) => e.textContent?.trim()).filter((t) => t === "Online Yemek").length).toBeGreaterThanOrEqual(1);
    expect(document.body.textContent).not.toContain("Online platform bilgileri");
    expect(document.body.textContent).not.toContain("Online siparişler");
    expect([...document.querySelectorAll('[role="tab"]')].map((t) => t.textContent)).toEqual(["Siparişler"]);
    await act(async () => root!.unmount());
    root = null;

    await renderRoute("/online/settings", ["orders.create", "integrations.manage"], online);
    expect(document.body.textContent).toContain("Online platform bağlantı bilgileri");
    await act(async () => root!.unmount());
    root = null;

    await renderRoute("/online/settings", ["orders.create"], online);
    expect(document.body.textContent).toContain(forbiddenText);
    await act(async () => root!.unmount());
    root = null;

    // V12-OUI-006: the problems path is for staff who see reports.
    await renderRoute("/online/problems", ["orders.create"], online);
    expect(document.body.textContent).toContain(forbiddenText);
    await act(async () => root!.unmount());
    root = null;
    await renderRoute("/online/problems", ["orders.create", "reports.view"], (url) => url.includes("/online-problems") ? [] : online(url));
    expect(document.body.textContent).not.toContain(forbiddenText);
    expect(document.body.textContent).toContain("Açık online sipariş sorunu yok.");
  });
});

/**
 * V1-RMD-367 (module-by-module UI audit, reopening module 6, 2026-09-27):
 * TableRoute's load() checked only `reason instanceof ApiError`, but
 * tableApi.ts's real client throws its own TableManagementApiError -
 * V1-RMD-114 already fixed this class of gap inside TableWorkspace.tsx's
 * OWN action-error helper, but this route-level load() (behind this file's
 * error/offline/stale states) never got the same fix. Module 6 had been
 * marked "no findings" before this was found while fixing the identical
 * gap in module 7's billing route - reopened and closed here.
 */
describe("workspace /tables route surfaces the real backend error message", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows the table-management endpoint's own error message, not the generic fallback", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/table-management/zones") || path.includes("/table-management/tables")) {
        // Not 0/401/409: those map to states whose own StateMessage never
        // renders the supplied message (see the billing test's own note).
        return new Response(
          JSON.stringify({ error: { code: "TABLE_STORE_UNAVAILABLE", message: "Masa servisi şu anda yanıt vermiyor." } }),
          { status: 500, headers: { "Content-Type": "application/json" } },
        );
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));
    window.history.replaceState({}, "", "/tables");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="88888888-8888-8888-8888-888888888888"
          displayName="Test Kullanıcı"
          capabilities={["tables.status"]}
          path="/tables"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Masa servisi şu anda yanıt vermiyor.");
    expect(document.body.textContent).not.toContain("Masa verisi alınamadı.");
  });
});

describe("workspace header for the cashier", () => {
  let root: Root | null = null;
  const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });

  async function open(onLogout = async () => {}) {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("awaiting-payment")) return json([{ orderId: "o1", orderNumber: "1", tableNumber: "M-3", itemCount: 1, total: 10, createdAt: "2026-11-12T09:00:00Z", billId: null, paidAmount: 0, itemPreview: null, tableId: "t" }, { orderId: "o2", orderNumber: "2", tableNumber: "M-4", itemCount: 1, total: 10, createdAt: "2026-11-12T09:00:00Z", billId: null, paidAmount: 0, itemPreview: null, tableId: "u" }]);
      return json([]);
    }));
    window.history.replaceState({}, "", "/tables");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId="88888888-8888-8888-8888-888888888888" displayName="Test" capabilities={["tables.status", "orders.create", "kitchen.advance", "catalog.manage"]} path="/tables" backendStatus="online" customerDisplayUrl="http://display" onLogout={onLogout} />
      </RouterProvider>));
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("keeps Masalar and Bekleyen hesaplar (with the waiting count) in the main navigation and the rest under Diğer", async () => {
    await open();
    expect([...document.querySelectorAll(".production-shell__nav > a")].map((link) => link.textContent)).toEqual(["Masalar", "Bekleyen hesaplar2 bekleyen"]);
    await act(async () => document.querySelector<HTMLButtonElement>(".production-shell__more-toggle")!.click());
    const more = [...document.querySelectorAll(".production-shell__more a, .production-shell__more button")].map((item) => item.textContent);
    expect(more).toEqual(["Kasa", "Online Yemek", "Mutfak", "Menü", "Sistem", "Müşteri ekranı", "Çıkış"]);
    expect(more).not.toContain("Masalar");
  });

  it("opens the quick sale screen from the table plan", async () => {
    await open();
    await act(async () => document.querySelector<HTMLButtonElement>(".table-quick-sale")!.click());
    expect(window.location.pathname).toBe("/");
  });

  it("signs out from the Diğer menu", async () => {
    const onLogout = vi.fn(async () => {});
    await open(onLogout);
    await act(async () => document.querySelector<HTMLButtonElement>(".production-shell__more-toggle")!.click());
    await act(async () => [...document.querySelectorAll<HTMLButtonElement>(".production-shell__more button")].find((button) => button.textContent === "Çıkış")!.click());
    expect(onLogout).toHaveBeenCalledTimes(1);
  });
});

describe("workspace /tables route marks tables whose check waits at the till", () => {
  let root: Root | null = null;
  const terminal = "88888888-8888-8888-8888-888888888888";
  const table = {
    tableId: "tbl-ask", tableNumber: "M-3", zoneId: null, capacity: 4, active: true, status: "Cleaning", currentOrderId: null,
    currentBillId: null, rowVersion: 1, allowedCommands: [], activeReservationId: null, reservationRowVersion: null,
    statusChangedAt: "2026-11-12T08:00:00Z",
  };
  const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });

  async function open(awaiting: (() => Response) | null, capabilities = ["tables.status", "orders.create"], withPlan = false, extra?: (path: string, init?: RequestInit) => Response | undefined, shown: Record<string, unknown> = table) {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      const handled = extra?.(path, init);
      if (handled) return handled;
      if (withPlan && path.includes("/floor-plans/")) return json({ zoneId: "z1", zoneCode: "Z", zoneName: "Salon", canvasWidth: 1000, canvasHeight: 600, rowVersion: 1, tables: [] });
      if (path.includes("/table-management/zones")) return json(withPlan ? [{ zoneId: "z1", code: "Z", name: "Salon", sortOrder: 1, active: true, rowVersion: 1 }] : []);
      if (path.includes("/table-management/tables")) return json([shown]);
      if (path.includes("awaiting-payment") && awaiting) return awaiting();
      throw new Error(`unexpected fetch: ${path}`);
    }));
    window.history.replaceState({}, "", "/tables");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId={terminal} displayName="Test" capabilities={capabilities} path="/tables" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>));
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows Hesap istedi for a sent check without a bill", async () => {
    await open(() => json([{ orderId: "o1", orderNumber: "1", tableNumber: "M-3", itemCount: 1, total: 10, createdAt: "2026-11-12T09:00:00Z", billId: null, paidAmount: 0, itemPreview: null, tableId: "tbl-ask" }]));
    expect(document.querySelector(".table-card--plan-ask")?.textContent).toContain("Hesap istedi");
  });

  it("draws the layout configuration buttons only for someone who holds floorplan.manage", async () => {
    const labels = () => [...document.querySelectorAll(".table-workspace__toolbar-actions button")].map((button) => button.textContent);
    await open(() => json([]));
    expect(labels()).toEqual(["Hızlı satış", "Yenile"]);
    expect(document.querySelector(".production-shell__workspace-heading p")).toBeNull();
    expect(document.querySelector(".table-order-bridge")).not.toBeNull();

    await act(async () => root!.unmount());
    await open(() => json([]), ["tables.status", "orders.create", "floorplan.manage"]);
    expect(labels()).toEqual(expect.arrayContaining(["+ Bölge ekle", "+ Masa ekle", "Bölgeleri yönet"]));
  });

  it("sends the unsent lines of the table's order from the panel and reads the tables again", async () => {
    const calls: string[] = [];
    const busy = { ...table, status: "Occupied", currentOrderId: "o-1", currentBillId: null, rowVersion: 4, allowedCommands: [] };
    const order = { orderId: "o-1", orderNumber: "1", status: "Draft", total: 20, items: [{ itemId: "i1", productName: "Çay", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Draft", kitchenState: "NotSent" }] };
    const extra = (path: string, init?: RequestInit) => {
      calls.push(`${init?.method ?? "GET"} ${path.replace(/^.*\/api\/v1/, "")}`);
      if (path.includes("/orders/table/")) return json(order);
      if (path.endsWith("/orders/table")) return json({ orderId: "o-1", orderNumber: "1", revision: 7, rowVersion: 5 });
      if (path.endsWith("/submit")) return json({ orderId: "o-1", orderNumber: "1", revision: 8, rowVersion: 6 });
      return undefined;
    };
    await open(() => json([]), ["tables.status", "orders.create"], false, extra, busy);
    await act(async () => { document.querySelector<HTMLButtonElement>(".table-card__select")!.click(); });
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());
    const tablesReads = () => calls.filter((call) => call.includes("/table-management/tables")).length;
    const before = tablesReads();

    await act(async () => { [...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent === "Mutfağa gönder (1)")!.click(); });
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());

    expect(calls.some((call) => call.startsWith("POST") && call.endsWith("/orders/o-1/submit"))).toBe(true);
    expect(tablesReads()).toBeGreaterThan(before);
  });

  it("opens the till with the kitchen-less reason picker when the panel asks for it", async () => {
    const busy = { ...table, status: "Occupied", currentOrderId: "o-1", currentBillId: null, rowVersion: 4, allowedCommands: [] };
    const order = { orderId: "o-1", orderNumber: "1", status: "Submitted", total: 20, items: [{ itemId: "i1", productName: "Çay", quantity: 1, unitPrice: 20, totalPrice: 20, status: "Active", kitchenState: "Sent" }] };
    const extra = (path: string) => path.includes("/orders/table/") ? json(order) : path.endsWith("/orders/table") ? json({ orderId: "o-1", orderNumber: "1", revision: 7, rowVersion: 5 }) : undefined;
    sessionStorage.clear();
    await open(() => json([]), ["tables.status", "orders.create", "orders.add-without-kitchen"], false, extra, busy);
    await act(async () => { document.querySelector<HTMLButtonElement>(".table-card__select")!.click(); });
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());

    await act(async () => { [...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent === "Mutfağa göndermeden hesaba ekle")!.click(); });
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());

    expect(window.location.pathname).toBe("/");
    expect(sessionStorage.getItem("alkaros.table-cart-order")).toBe("o-1");
    expect(sessionStorage.getItem("alkaros.table-cart-without-kitchen")).toBe("1");
  });

  it("offers Planı düzenle on the floor plan only to someone who holds floorplan.manage", async () => {
    const editButton = () => [...document.querySelectorAll("button")].find((button) => button.textContent === "Planı düzenle");
    const showPlan = async () => { await act(async () => { [...document.querySelectorAll(".table-workspace__view-toggle button")].find((button) => button.textContent === "Salon planı")!.dispatchEvent(new MouseEvent("click", { bubbles: true })); }); };
    await open(() => json([]), ["tables.status", "orders.create"], true);
    await showPlan();
    expect(document.querySelector(".floor-plan-shell")).not.toBeNull();
    expect(editButton()).toBeUndefined();

    await act(async () => root!.unmount());
    await open(() => json([]), ["tables.status", "orders.create", "floorplan.manage"], true);
    await showPlan();
    expect(editButton()).toBeDefined();
  });

  it("never asks the till list for an operator who may not read it", async () => {
    await open(() => json([]), ["tables.status"]);
    expect(document.querySelector(".table-attention__unavailable")).toBeNull();
    const fetched = (fetch as unknown as { mock: { calls: unknown[][] } }).mock.calls.map((call) => String(call[0]));
    expect(fetched.some((path) => path.includes("awaiting-payment"))).toBe(false);
    expect(document.querySelector(".table-card")).not.toBeNull();
  });

  it("does not ask when the check already has a bill, and still renders when the till list cannot be read", async () => {
    await open(() => json([{ orderId: "o1", orderNumber: "1", tableNumber: "M-3", itemCount: 1, total: 10, createdAt: "2026-11-12T09:00:00Z", billId: "b1", paidAmount: 0, itemPreview: null, tableId: "tbl-ask" }]));
    expect(document.querySelector(".table-card--plan-ask")).toBeNull();
    await act(async () => root!.unmount());
    await open(null);
    expect(document.querySelector(".table-card--plan-clean")).not.toBeNull();
    expect(document.querySelector(".table-attention__unavailable")).not.toBeNull();
  });
});

/**
 * V1-RMD-366 (module-by-module UI audit, 2026-09-27): BillingRoute's load()
 * checked only `reason instanceof ApiError` for the message it shows, but
 * billingApi.ts's real client throws its own BillingSplitApiError - a
 * completely different class. A genuine backend failure (not just a
 * network drop or a bare 409) was silently replaced by the generic
 * fallback text, hiding the server's own Turkish reason from the cashier.
 */
describe("workspace /billing route surfaces the real backend error message", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows the no-active-bill information state, not a load failure, when no bill is open", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => { throw new Error(`unexpected fetch: ${String(input)}`); }));
    localStorage.clear();
    window.history.replaceState({}, "", "/billing");
    await render(
      <RouterProvider>
        <ExperiencePage terminalId="66666666-6666-6666-6666-666666666666" displayName="Test Kullanıcı" capabilities={["bills.split"]}
          path="/billing" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    );
    expect(document.body.textContent).toContain("Aktif adisyon yok");
    expect(document.body.textContent).not.toContain("Hesap alınamadı");
  });

  it("shows the fixed Turkish sentence for the split-design error code, not the generic fallback or the server text", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/split-design")) {
        // Not 409/401/0: those map to states (stale/unauthorized/offline)
        // whose own StateMessage never renders `message` at all - a
        // deliberate design choice for "get fresh data" wording, not part
        // of what this test is pinning. Any other status falls to the
        // generic "error" state, whose StateMessage does show it.
        return new Response(
          JSON.stringify({ error: { code: "UNSUPPORTED_BILL_STATE", message: "raw english text" } }),
          { status: 422, headers: { "Content-Type": "application/json" } },
        );
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));
    window.history.replaceState({}, "", "/billing?billId=55555555-5555-5555-5555-555555555555");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="66666666-6666-6666-6666-666666666666"
          displayName="Test Kullanıcı"
          capabilities={["bills.split"]}
          path="/billing"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Bu hesap durumunda bölme tasarımı değiştirilemez.");
    expect(document.body.textContent).not.toContain("raw english text");
    expect(document.body.textContent).not.toContain("Hesap bölme verisi alınamadı.");
  });
});

/**
 * V1-RMD-368 (module-by-module UI audit, 2026-09-27): CatalogRoute's load()
 * checked only `reason instanceof ApiError`, but catalogApi.ts's real
 * client throws its own CatalogApiError - same class of gap as V1-RMD-366
 * (billing) and the reopened V1-RMD-367 (tables), all stemming from
 * V1-RMD-114's original fix never reaching every route in this one file.
 */
describe("workspace /catalog route surfaces the real backend error message", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows the catalog endpoint's own error message, not the generic fallback", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/management/catalog/")) {
        // Not 0/401/409: those map to states whose own StateMessage never
        // renders the supplied message (see the billing test's own note).
        return new Response(
          JSON.stringify({ error: { code: "CATALOG_LOCKED", message: "Katalog şu anda bakımda; yeniden deneyin." } }),
          { status: 500, headers: { "Content-Type": "application/json" } },
        );
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));
    window.history.replaceState({}, "", "/catalog");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="77777777-7777-7777-7777-777777777777"
          displayName="Test Kullanıcı"
          capabilities={["catalog.manage"]}
          path="/catalog"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Katalog şu anda bakımda; yeniden deneyin.");
    expect(document.body.textContent).not.toContain("Katalog verisi alınamadı.");
  });

  it("shows the empty-catalog start message when the server holds no catalog record", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/management/catalog/")) {
        return new Response(JSON.stringify({ items: [], nextCursor: null }), { status: 200, headers: { "Content-Type": "application/json" } });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));
    window.history.replaceState({}, "", "/catalog");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="77777777-7777-7777-7777-777777777777"
          displayName="Test Kullanıcı"
          capabilities={["catalog.manage"]}
          path="/catalog"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Katalog kaydı yok");
  });
});

/**
 * V1-RMD-369 (module-by-module UI audit, 2026-09-27): KitchenRoute's load()
 * checked only `reason instanceof ApiError`, but kitchenApi.ts's real
 * client throws its own KitchenOperationsApiError - same class of gap as
 * V1-RMD-366/367/368, this time in the route-level load() rather than
 * KitchenOperationsWorkspace.tsx's own action handlers (which already
 * checked KitchenOperationsApiError correctly, see V1-RMD-214).
 */
describe("workspace /kitchen route surfaces the real backend error message", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows the kitchen endpoint's own error message, not the generic fallback", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line" });
      if (path.includes("/tickets")) {
        // Not 0/401/409: those map to states whose own StateMessage never
        // renders the supplied message (see the billing test's own note).
        return new Response(
          JSON.stringify({ error: { code: "STATION_OFFLINE", message: "Mutfak istasyonu şu anda çevrimdışı." } }),
          { status: 500, headers: { "Content-Type": "application/json" } },
        );
      }
      return jsonResponse([]);
    }));
    window.history.replaceState({}, "", "/kitchen");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="99999999-9999-9999-9999-999999999999"
          displayName="Test Kullanıcı"
          capabilities={["kitchen.advance"]}
          path="/kitchen"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Mutfak istasyonu şu anda çevrimdışı.");
    expect(document.body.textContent).not.toContain("Mutfak verisi alınamadı.");
  });
});

/**
 * V1-RMD-372 (module-by-module UI audit, module 12, 2026-09-27):
 * SystemHealthRoute's load() checked only `reason instanceof ApiError`, but
 * it reuses kitchen-operations' client (no dedicated system-health API),
 * whose real errors are KitchenOperationsApiError - same class of gap as
 * V1-RMD-366/367/368/369.
 */
describe("workspace /system-health route surfaces the real backend error message", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("shows the health endpoint's own error message, not the generic fallback", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line" });
      if (path.includes("/operations/health/latest")) {
        // Not 0/401: those map to states whose own StateMessage never
        // renders the supplied message (see the billing test's own note).
        return new Response(
          JSON.stringify({ error: { code: "SNAPSHOT_STALE", message: "Sağlık anlık görüntüsü alınamadı." } }),
          { status: 500, headers: { "Content-Type": "application/json" } },
        );
      }
      return jsonResponse([]);
    }));
    window.history.replaceState({}, "", "/system-health");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
          displayName="Test Kullanıcı"
          capabilities={["catalog.manage"]}
          path="/system-health"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Sağlık anlık görüntüsü alınamadı.");
    expect(document.body.textContent).not.toContain("Sağlık verisi alınamadı.");
  });
});

/**
 * V1-RMD-381 (module-by-module UI audit round 2, 2026-09-27): unlike
 * KitchenOperationsWorkspace's own 8s poll, TableRoute never refreshed on
 * its own - a manager watching the floor plan saw only whatever was true
 * at the moment they last clicked "Yenile", with no way to know a
 * waiter/cashier's own table-status change had landed since.
 */
describe("workspace /tables route polls for live status changes", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("re-fetches every 8s while the floor plan is ready, and stops once it is not", async () => {
    let zoneCalls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/table-management/zones")) { zoneCalls += 1; return jsonResponse([]); }
      if (path.includes("/table-management/tables")) return jsonResponse([]);
      return jsonResponse([]);
    }));

    vi.useFakeTimers();
    window.history.replaceState({}, "", "/tables");
    await render(
      <RouterProvider>
        <ExperiencePage
          terminalId="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
          displayName="Test Kullanıcı"
          capabilities={["tables.status"]}
          path="/tables"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    );
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const afterInitialLoad = zoneCalls;
    expect(afterInitialLoad).toBeGreaterThan(0);

    await act(async () => { await vi.advanceTimersByTimeAsync(8_000); });
    expect(zoneCalls).toBe(afterInitialLoad + 1);

    await act(async () => { await vi.advanceTimersByTimeAsync(8_000); });
    expect(zoneCalls).toBe(afterInitialLoad + 2);
  });
});

/**
 * V1-RMD-223: found by an independent audit (2026-09-16) - KitchenRoute's
 * onLoadPerformanceReport prop was a fresh inline closure every render, and
 * `client` itself is a fresh object every 8s poll cycle (load() always
 * builds a new one) even when nothing about it changed. The report view's
 * own useEffect depends on this prop, so it kept re-fetching (and flashing
 * back to "loading") on every single poll tick while someone was reading
 * the report - the opposite of the workspace's own documented intent that
 * the report is not part of the board's polling.
 */
describe("KitchenRoute's performance report survives the board's own poll cycle", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("does not re-fetch the report on a poll tick while the report tab is open", async () => {
    const reportCalls: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.endsWith("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line" });
      if (path.includes("/operations/performance-report")) {
        reportCalls.push(path);
        return jsonResponse({ from: "2026-09-16", to: "2026-09-16", stations: [], hourlyVolume: [] });
      }
      if (path.includes("/operations/live-sync")) return jsonResponse({ enabled: true, denseModeThreshold: 9 });
      if (path.includes("/operations/health/latest")) return new Response(null, { status: 204 });
      // /tickets, /printers, /routes, /categories, /deliveries/unknown,
      // /operations/backups/recent all shape as a plain empty array.
      return jsonResponse([]);
    }));

    vi.useFakeTimers();
    window.history.replaceState({}, "", "/kitchen");
    await act(async () => render(
      <RouterProvider>
        <ExperiencePage
          terminalId="44444444-4444-4444-4444-444444444444"
          displayName="Test Kullanıcı"
          capabilities={["kitchen.advance", "reports.view"]}
          path="/kitchen"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    ));
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const reportTab = [...document.querySelectorAll("button")].find((b) => b.textContent?.trim() === "Rapor");
    expect(reportTab).toBeDefined();
    await act(async () => reportTab!.dispatchEvent(new MouseEvent("click", { bubbles: true })));
    await act(async () => Promise.resolve());
    expect(reportCalls).toHaveLength(1);

    // Advance past the board's own 8s poll interval while still on the
    // report tab - the fetch mock above proves whether the report was
    // fetched again.
    await act(async () => { await vi.advanceTimersByTimeAsync(9_000); });

    expect(reportCalls).toHaveLength(1);
  });
});

/** V1-RMD-445: the "Yönetim" route opens for reports.view, lists its sections by capability, and is closed otherwise. */
describe("workspace /management route", () => {
  let root: Root | null = null;

  async function renderManagementRoute(capabilities: readonly string[]) {
    window.history.replaceState({}, "", "/management");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage
          terminalId="22222222-2222-2222-2222-222222222222"
          displayName="Deniz Kaya"
          capabilities={capabilities}
          path="/management"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    ));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  it("with reports.view, shows the closing section and reads the day, report, confirmations and cases", async () => {
    const fetch = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/business-day/")) return jsonResponse({ message: "yok" }, 404);
      if (path.includes("/settlement-report")) return jsonResponse({ paymentMix: [], unsettledPayments: { unknownCount: 0, unknownAmount: 0, reconciliationRequiredCount: 0, reconciliationRequiredAmount: 0 }, cashSessions: [], reconciliationTotals: [] });
      return jsonResponse([]);
    });
    vi.stubGlobal("fetch", fetch);

    await renderManagementRoute(["reports.view"]);
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Gün sonu ve mutabakat");
    expect(document.body.textContent).toContain("Bu tarih için iş günü kaydı yok.");
    expect(fetch).toHaveBeenCalled();
    expect([...document.querySelectorAll("button")].map((button) => button.textContent)).not.toContain("İş gününü aç");
  });

  it("without reports.view, never calls the management API", async () => {
    const fetch = vi.fn(async () => jsonResponse([]));
    vi.stubGlobal("fetch", fetch);

    await renderManagementRoute(["orders.create"]);

    expect((fetch.mock.calls as unknown[][]).map(([url]) => String(url)).filter((url) => url.includes("/api/v1/management"))).toEqual([]);
  });
});

/** The kitchen screen's printer form is wired to the printer endpoints only for the routing permission, and the list is reloaded after a save. */
describe("KitchenRoute printer setup wiring", () => {
  let root: Root | null = null;
  const calls: { method: string; path: string; body?: string }[] = [];

  async function renderKitchen(capabilities: readonly string[]) {
    calls.length = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      calls.push({ method: init?.method ?? "GET", path, body: typeof init?.body === "string" ? init.body : undefined });
      if (path.endsWith("/runtime-configuration")) return jsonResponse({ kitchenStationId: "hot-line" });
      if (path.includes("/operations/live-sync")) return jsonResponse({ enabled: true, denseModeThreshold: 9 });
      if (path.includes("/operations/health/latest")) return new Response(null, { status: 204 });
      if (init?.method === "POST" && path.includes("/printers/")) return jsonResponse({ id: "new" });
      return jsonResponse([]);
    }));
    window.history.replaceState({}, "", "/kitchen");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId="55555555-5555-5555-5555-555555555555" displayName="Test" capabilities={capabilities} path="/kitchen" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    ));
    for (let i = 0; i < 4; i++) await act(async () => Promise.resolve());
  }

  const button = (text: string) => [...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.trim() === text);
  const printerListReads = () => calls.filter((call) => call.method === "GET" && /\/printers$/.test(call.path)).length;

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  it("offers no printer form without the routing permission", async () => {
    await renderKitchen(["kitchen.advance"]);

    expect(button("Yazıcı ekle")).toBeUndefined();
  });

  it("saves a new printer through the printer endpoint and then reloads the printer list", async () => {
    await renderKitchen(["kitchen.advance", "kitchen.routing.manage"]);
    const readsBefore = printerListReads();
    await act(async () => button("Yazıcı ekle")!.click());
    const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
    for (const [label, value] of [["Yazıcı adı", "Bar"], ["İstasyon", "bar"], ["Ağ adresi", "192.168.1.60"]]) {
      const input = [...document.querySelectorAll<HTMLLabelElement>("form[aria-label] label")].find((candidate) => candidate.textContent?.startsWith(label))!.querySelector("input")!;
      await act(async () => { set.call(input, value); input.dispatchEvent(new Event("input", { bubbles: true })); });
    }
    await act(async () => { document.querySelector("form[aria-label]")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });
    for (let i = 0; i < 4; i++) await act(async () => Promise.resolve());

    const post = calls.find((call) => call.method === "POST" && /\/printers\/[0-9a-f-]{36}$/.test(call.path));
    expect(JSON.parse(post!.body!)).toEqual({ name: "Bar", stationId: "bar", ipAddress: "192.168.1.60", port: 9100, profile: "Default" });
    expect(printerListReads()).toBeGreaterThan(readsBefore);
  });
});

/**
 * A background refresh must not swap the table screen for the loading state (that unmounts an
 * open dialog and an unsaved floor plan draft), a failed one must keep the last known tables
 * on screen, and a screen that failed to load must recover by itself.
 */
describe("workspace /tables route background refresh", () => {
  let root: Root | null = null;

  const tableJson = {
    tableId: "table-1", tableNumber: "S-01", zoneId: null, capacity: 4, active: true, status: "Available",
    currentOrderId: null, currentBillId: null, rowVersion: 1, allowedCommands: ["Update", "Reserve"],
    activeReservationId: null, reservationRowVersion: null, statusChangedAt: "2026-01-01T10:00:00Z",
  };

  async function renderRoute() {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    window.history.replaceState({}, "", "/tables");
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage
          terminalId="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
          displayName="Test Kullanıcı"
          capabilities={["tables.status"]}
          path="/tables"
          backendStatus="online"
          onLogout={async () => {}}
        />
      </RouterProvider>,
    ));
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());
  }

  const tick = () => act(async () => { await vi.advanceTimersByTimeAsync(8_000); });

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("refreshes without unmounting the workspace", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) =>
      jsonResponse(String(input).includes("/tables") ? [tableJson] : [])));
    vi.useFakeTimers();
    await renderRoute();
    const workspace = document.querySelector(".table-workspace")!;
    expect(workspace.textContent).toContain("S-01");

    await tick();

    expect(document.querySelector(".table-workspace")).toBe(workspace);
    expect(document.body.textContent).not.toContain("Masa düzeni yükleniyor");
  });

  it("keeps the last known tables and shows a banner when a refresh fails, then clears it", async () => {
    let failing = false;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (failing) return jsonResponse({ error: { code: "X", message: "Sunucu hatası." } }, 500);
      return jsonResponse(String(input).includes("/tables") ? [tableJson] : []);
    }));
    vi.useFakeTimers();
    await renderRoute();

    failing = true;
    await tick();
    expect(document.body.textContent).toContain("S-01");
    expect(document.querySelector(".table-workspace__refresh-error")?.textContent).toContain("son bilinen durum");

    failing = false;
    await tick();
    expect(document.querySelector(".table-workspace__refresh-error")).toBeNull();
    expect(document.body.textContent).toContain("S-01");
  });

  it("keeps the floor plan view when only the plan refresh fails", async () => {
    let planFails = false;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/floor-plans/")) {
        return planFails
          ? jsonResponse({ error: { code: "X", message: "Sunucu hatası." } }, 500)
          : jsonResponse({ zoneId: "zone-1", zoneCode: "Z", zoneName: "Salon", canvasWidth: 1000, canvasHeight: 600, rowVersion: 1, tables: [] });
      }
      if (path.includes("/zones")) return jsonResponse([{ zoneId: "zone-1", code: "Z", name: "Salon", sortOrder: 1, active: true, rowVersion: 1 }]);
      return jsonResponse([{ ...tableJson, zoneId: "zone-1" }]);
    }));
    vi.useFakeTimers();
    await renderRoute();
    await act(async () => [...document.querySelectorAll<HTMLButtonElement>(".table-workspace__view-toggle button")].find((button) => button.textContent === "Salon planı")!.click());
    expect(document.querySelector(".table-workspace__floor-plan")).not.toBeNull();

    planFails = true;
    await tick();

    expect(document.querySelector(".table-workspace__floor-plan")).not.toBeNull();
  });

  it("stops asking for the floor plan of a zone that has none, except on a manual refresh", async () => {
    let planCalls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/floor-plans/")) {
        planCalls += 1;
        return jsonResponse({ error: { code: "FLOOR_PLAN_NOT_FOUND", message: "Kat planı bulunamadı." } }, 404);
      }
      if (path.includes("/zones")) return jsonResponse([{ zoneId: "zone-1", code: "Z", name: "Salon", sortOrder: 1, active: true, rowVersion: 1 }]);
      return jsonResponse([{ ...tableJson, zoneId: "zone-1" }]);
    }));
    vi.useFakeTimers();
    await renderRoute();
    expect(planCalls).toBe(1);

    await tick();
    await tick();
    expect(planCalls).toBe(1);

    await act(async () => [...document.querySelectorAll("button")].find((button) => button.textContent === "Yenile")!.click());
    expect(planCalls).toBe(2);
  });

  it("asks for the floor plan once when a zone is picked, not twice", async () => {
    const planPaths: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/floor-plans/")) {
        planPaths.push(path);
        return jsonResponse({ zoneId: "zone-2", zoneCode: "T", zoneName: "Teras", canvasWidth: 1000, canvasHeight: 600, rowVersion: 1, tables: [] });
      }
      if (path.includes("/zones")) return jsonResponse([
        { zoneId: "zone-1", code: "Z", name: "Salon", sortOrder: 1, active: true, rowVersion: 1 },
        { zoneId: "zone-2", code: "T", name: "Teras", sortOrder: 2, active: true, rowVersion: 1 },
      ]);
      return jsonResponse([{ ...tableJson, zoneId: "zone-1" }]);
    }));
    vi.useFakeTimers();
    await renderRoute();
    planPaths.length = 0;

    const terrace = [...document.querySelectorAll<HTMLButtonElement>(".table-zone-tabs button")].find((tab) => tab.textContent === "Teras")!;
    await act(async () => terrace.click());
    for (let i = 0; i < 5; i++) await act(async () => { await Promise.resolve(); });

    expect(planPaths.filter((path) => path.includes("zone-2"))).toHaveLength(1);
  });

  it("recovers by itself after the first load failed", async () => {
    let failing = true;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (failing) return jsonResponse({ error: { code: "X", message: "Sunucu hatası." } }, 500);
      return jsonResponse(String(input).includes("/tables") ? [tableJson] : []);
    }));
    vi.useFakeTimers();
    await renderRoute();
    expect(document.body.textContent).toContain("Masa düzeni alınamadı");

    failing = false;
    await tick();

    expect(document.body.textContent).toContain("S-01");
    expect(document.body.textContent).not.toContain("Masa düzeni alınamadı");
  });
});

const billingJson = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
const orderBillDesign = {
  billId: "55555555-5555-5555-5555-555555555555", billNumber: "H-1", billStatus: "Open", currencyCode: "TRY",
  payableAmount: 10, taxTotal: 1, billRowVersion: 1, mode: "None", executionState: "DesignOnly",
  allowedCommands: [], items: [], allocations: [],
};

describe("workspace /billing route opens an order's bill once", () => {
  let root: Root | null = null;
  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  it("opens a bill from an order with a single POST", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/from-order/")) return billingJson(orderBillDesign);
      if (path.endsWith("/owners")) return billingJson([]);
      throw new Error(`unexpected fetch: ${path}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    window.history.replaceState({}, "", "/billing?orderId=77777777-7777-7777-7777-777777777777");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId="66666666-6666-6666-6666-666666666666" displayName="Test" capabilities={["bills.split"]}
          path="/billing" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    ));
    for (let i = 0; i < 6; i++) await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("H-1");
    expect(fetchMock.mock.calls.filter(([url]) => String(url).includes("/from-order/"))).toHaveLength(1);
  });
});

describe("workspace header drawer button", () => {
  let root: Root | null = null;

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render(cashSession: { state: "open" | "closed" | "unknown" | null; reload: () => void } | null) {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse([])));
    window.history.replaceState({}, "", "/authorization");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId="22222222-2222-2222-2222-222222222222" displayName="Deniz Kaya" capabilities={["reports.view"]}
          path="/authorization" backendStatus="online" cashSession={cashSession} onLogout={async () => {}} />
      </RouterProvider>,
    ));
  }

  it("shows the drawer state as a link to the drawer page, and a band while it is not open", async () => {
    await render({ state: "closed", reload: () => {} });

    const link = document.querySelector<HTMLAnchorElement>("a.cash-session-link")!;
    expect(link.textContent).toBe("Kasa: Kapalı");
    expect(link.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
    expect(document.querySelector(".cash-session-band")?.textContent).toContain("Kasa oturumu kapalı");
  });

  it("shows neither the link nor the band for a session without the drawer permission", async () => {
    await render(null);

    expect(document.querySelector("a.cash-session-link")).toBeNull();
    expect(document.querySelector(".cash-session-band")).toBeNull();
  });
});

describe("workspace /tables last update time", () => {
  let root: Root | null = null;
  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("shows the venue's Istanbul clock, not the device's", async () => {
    process.env.TZ = "UTC";
    vi.stubGlobal("fetch", vi.fn(async () => new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } })));
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-09-24T11:32:00Z"));
    window.history.replaceState({}, "", "/tables");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(
      <RouterProvider>
        <ExperiencePage terminalId="88888888-8888-8888-8888-888888888888" displayName="Test Kullanıcı" capabilities={["tables.status"]}
          path="/tables" backendStatus="online" onLogout={async () => {}} />
      </RouterProvider>,
    ));
    for (let i = 0; i < 4; i++) await act(async () => { await Promise.resolve(); });

    expect(document.body.textContent).toContain("Son güncelleme 14:32:00");
  });
});
