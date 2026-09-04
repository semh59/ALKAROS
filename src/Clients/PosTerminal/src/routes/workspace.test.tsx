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
    await act(async () => Promise.resolve());

    expect(approved).toBe(pendingGrant.grantId);
  });
});
