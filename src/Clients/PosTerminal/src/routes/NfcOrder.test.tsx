// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NfcOrder } from "./NfcOrder";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const CATALOG = [
  { productId: "p1", sku: "corba", name: "Çorba", categoryCode: "SOUP", categoryName: "Çorba", unitPrice: 60, taxRate: 10 },
  { productId: "p2", sku: "kofte", name: "Köfte", categoryCode: "MAIN", categoryName: "Ana Yemek", unitPrice: 280, taxRate: 10 },
];

/**
 * V12-NFC-003: the customer-facing NFC order page (`/nfc/{tableId}`). No
 * login of any kind — every fetch here is anonymous, matching the real
 * `V12-NFC-001` API it calls.
 */
describe("NfcOrder", () => {
  let root: Root | null = null;

  async function render(path: string) {
    window.history.pushState(null, "", path);
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<NfcOrder /> as ReactElement));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    sessionStorage.clear();
  });

  it("with an invalid link (no table id), shows a clear error instead of a broken menu", async () => {
    await render("/nfc/");

    expect(document.body.textContent).toContain("Bu bağlantı geçersiz");
  });

  it("loads and renders the menu for a valid table link", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(CATALOG)));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Çorba");
    expect(document.body.textContent).toContain("Köfte");
  });

  it("adding items shows the cart bar with the running total, and placing the order shows the confirmation screen", async () => {
    const placedOrder = {
      orderId: "o1",
      tableId: "table-1",
      tableNumber: "T-01",
      status: "Accepted",
      rowVersion: 3,
      totalAmount: 60,
      items: [
        { itemId: "i1", productId: "p1", productName: "Çorba", quantity: 1, unitPrice: 60, totalPrice: 60, specialInstructions: null },
      ],
      createdAt: new Date().toISOString(),
    };
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) return jsonResponse(placedOrder);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());

    const increaseCorba = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increaseCorba.click());

    expect(document.body.textContent).toContain("Siparişi Gönder");

    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("Siparişiniz alındı");
    expect(document.body.textContent).toContain("Çorba");
  });

  // V1-RMD-348 (independent 2026-09-26 audit, orta seviye bulgu): before this,
  // submissionIdRef lived only in a React ref - a page reload/navigation away
  // while a submission was in flight lost it entirely, so a retry generated
  // a brand new id and could place a genuine duplicate order. This seeds
  // sessionStorage exactly as a real prior mount (interrupted mid-submission
  // by a reload) would have left it, then mounts fresh - the real-world
  // scenario this fix targets, without the flakiness of trying to abandon a
  // genuinely in-flight React update mid-act().
  it("picks up a submissionId a previous (reloaded-away) mount already persisted, instead of generating a new one", async () => {
    sessionStorage.setItem("alkaros.nfc.submissionId.table-1", "existing-in-flight-id");
    let submittedId: string | null = null;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) {
        submittedId = (JSON.parse(String(init!.body)) as { id: string }).id;
        return jsonResponse({
          orderId: "o1", tableId: "table-1", tableNumber: "T-01", status: "Accepted", rowVersion: 3,
          totalAmount: 60, items: [], createdAt: new Date().toISOString(),
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());
    const increase = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increase.click());
    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(submittedId).toBe("existing-in-flight-id");
    // Placed successfully -> the key is cleared, so a genuinely NEW order
    // later gets a fresh id rather than replaying this one forever.
    expect(sessionStorage.getItem("alkaros.nfc.submissionId.table-1")).toBeNull();
  });

  it("two browser tabs open on different tables never collide on the same submissionId key", async () => {
    sessionStorage.setItem("alkaros.nfc.submissionId.table-2", "other-tables-id");
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) {
        const id = (JSON.parse(String(init!.body)) as { id: string }).id;
        expect(id).not.toBe("other-tables-id");
        return jsonResponse({
          orderId: "o1", tableId: "table-1", tableNumber: "T-01", status: "Accepted", rowVersion: 3,
          totalAmount: 60, items: [], createdAt: new Date().toISOString(),
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());
    const increase = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increase.click());
    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());
  });

  it("when the table is not available for self-service, shows the blocking message from the server", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      if (path.includes("/orders")) {
        return jsonResponse(
          { error: { code: "TABLE_NOT_AVAILABLE", message: "Bu masada şu anda kendi kendine sipariş verilemiyor, lütfen garsonu çağırın." } },
          409,
        );
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render("/nfc/table-1");
    await act(async () => Promise.resolve());
    const increase = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
    await act(async () => increase.click());
    const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
    await act(async () => submitButton.click());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("garsonu çağırın");
  });

  // V1-RMD-389 (Tur 2, P2): a real customer's phone can lock, ring, or have
  // the OS reclaim the tab's memory mid-browsing far more often than a staff
  // terminal ever does. Before this fix, the cart lived only in React state,
  // so any such ordinary interruption before tapping submit silently wiped
  // everything the customer had picked.
  describe("cart survives an interrupted browsing session (V1-RMD-389)", () => {
    it("picks up a cart a previous (reloaded-away) mount already persisted", async () => {
      sessionStorage.setItem("alkaros.nfc.cart.table-1", JSON.stringify({ p2: 3 }));
      vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(CATALOG)));

      await render("/nfc/table-1");
      await act(async () => Promise.resolve());

      expect(document.body.textContent).toContain("Siparişi Gönder");
      const kofteDecrease = document.querySelector<HTMLButtonElement>('[aria-label="Köfte adedini azalt"]')!;
      const count = kofteDecrease.parentElement!.querySelector(".nfc-stepper__count");
      expect(count?.textContent).toBe("3");
    });

    it("clears the persisted cart once the order is placed, so a later visit starts fresh", async () => {
      vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
        const path = String(input);
        if (path.includes("/catalog")) return jsonResponse(CATALOG);
        if (path.includes("/orders")) {
          return jsonResponse({
            orderId: "o1", tableId: "table-1", tableNumber: "T-01", status: "Accepted", rowVersion: 3,
            totalAmount: 60, items: [], createdAt: new Date().toISOString(),
          });
        }
        throw new Error(`unexpected fetch: ${path}`);
      }));

      await render("/nfc/table-1");
      await act(async () => Promise.resolve());
      const increase = document.querySelector<HTMLButtonElement>('[aria-label="Çorba adedini artır"]')!;
      await act(async () => increase.click());
      expect(sessionStorage.getItem("alkaros.nfc.cart.table-1")).toBe(JSON.stringify({ p1: 1 }));

      const submitButton = [...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişi Gönder")!;
      await act(async () => submitButton.click());
      await act(async () => Promise.resolve());

      expect(document.body.textContent).toContain("Siparişiniz alındı");
      expect(sessionStorage.getItem("alkaros.nfc.cart.table-1")).toBeNull();
    });
  });

  // V1-RMD-374 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, this guest-facing screen had no axe-core scan.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS masa siparişi";
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/catalog")) return jsonResponse(CATALOG);
      throw new Error(`unexpected fetch: ${path}`);
    }));
    await render("/nfc/table-1");
    await act(async () => Promise.resolve());
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
