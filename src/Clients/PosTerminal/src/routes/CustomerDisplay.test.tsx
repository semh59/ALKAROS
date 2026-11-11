// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { formatMoney } from "../format";
import { readFileSync } from "node:fs";
import { join } from "node:path";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

// V1-RMD-374 (module-by-module UI audit, 2026-09-27): mirrors
// Cashier.help-alerts.test.tsx's own SignalR mock - the reason no other
// hub usage in this codebase is unit-tested at a deeper layer than "does it
// call start()".
// jsdom has no layout engine, so no ResizeObserver; the stub lets a test say "the box changed size".
const resizeObservers = new Set<() => void>();
class ResizeObserverStub {
  constructor(private readonly callback: () => void) {}
  observe() { resizeObservers.add(this.callback); }
  disconnect() { resizeObservers.delete(this.callback); }
}
(globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = ResizeObserverStub;

const startMock = vi.fn(async () => undefined);
const hub: { changed?: () => void; reconnected?: () => void; closed?: () => void } = {};
vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (_event: string, handler: () => void) => { hub.changed = handler; },
        onreconnecting: () => undefined,
        onreconnected: (handler: () => void) => { hub.reconnected = handler; },
        onclose: (handler: () => void) => { hub.closed = handler; },
        start: startMock,
        stop: async () => undefined,
      };
    }
  },
}));

const snapshot = {
  displayId: "d-1", terminalId: "t-1", orderId: "o-1", revision: 3, state: "Active", editable: true,
  orderNumber: "S-042",
  lines: [{ itemId: "i-1", name: "Köfte", quantity: 2, unitPrice: 280, lineTotal: 560, discountAmount: 0 }],
  subtotal: 560, discountTotal: 0, taxTotal: 50.91, total: 560, currency: "TRY",
  paidTotal: 0, remainingTotal: null, tipTotal: 0, feeTotal: 0,
  serverTimestamp: "2026-09-27T12:00:00Z", message: "Siparişiniz hazırlanıyor.",
};

/**
 * V1-RMD-374 (module-by-module UI audit, 2026-09-27): CustomerDisplay.tsx -
 * the actual customer-facing screen on the till's paired second monitor -
 * had no test file at all.
 */
describe("CustomerDisplay", () => {
  let root: Root | null = null;

  async function render() {
    const { CustomerDisplay } = await import("./CustomerDisplay");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(createElement(CustomerDisplay)));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.resetModules();
    startMock.mockReset();
    startMock.mockImplementation(async () => undefined);
    hub.changed = hub.reconnected = hub.closed = undefined;
    vi.useRealTimers();
  });

  const tick = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });
  const pairingBody = (code: string) => ({
    requestId: `r-${code}`, displayId: "d-1", secret: "s-1", code, expiresAt: new Date(Date.now() + 120_000).toISOString(),
    expiresInSeconds: 120,
  });

  it("shows the pairing code when the display has no session yet", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "Eşleştirme gerekli." } }, 401);
      if (path.includes("/pairing-requests")) {
        return jsonResponse({ requestId: "r-1", displayId: "d-1", secret: "s-1", code: "A7K2M9P4", expiresAt: new Date(Date.now() + 120_000).toISOString(), expiresInSeconds: 120 });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("A7K2M9P4");
    expect(document.body.textContent).toContain("kasaya bağlayın");
  });

  it("renders an active order's lines and total, authoritative from the snapshot", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse(snapshot);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("S-042");
    expect(document.body.textContent).toContain("Köfte");
    expect(document.body.textContent).toContain("560,00");
    expect(document.body.textContent).toContain("İçindeki KDV");
    expect(document.body.textContent).toContain("50,91");
    expect(document.body.textContent).not.toContain("616,00");
    expect(startMock).toHaveBeenCalled();
  });

  it("shows what is left to pay once part of the bill was taken", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) {
        return jsonResponse({
          ...snapshot, total: 240, paidTotal: 100, remainingTotal: 140, message: "Ödemenizin bir bölümü alındı; kalan tutar ödenecek.",
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const card = document.querySelector(".display-total-card")!.textContent!;
    expect(card).toContain("KALAN TUTAR");
    expect(card).toContain("140,00");
    expect(card).toContain("240,00");
    expect(card).toContain("Ödenen");
    expect(card).toContain("100,00");
    expect(card).toContain("Hesap toplamı");
    expect(card).not.toContain("ÖDENECEK TOPLAM");
    expect(card).toContain("kalan tutar ödenecek");
  });

  it("keeps the full total headline while nothing is paid yet", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, paidTotal: 0, remainingTotal: 560 });
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const card = document.querySelector(".display-total-card")!.textContent!;
    expect(card).toContain("ÖDENECEK TOPLAM");
    expect(card).not.toContain("Ödenen");
  });

  it("conceals a completed order's line detail after the thank-you screen, showing the welcome card instead", async () => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, state: "Completed", editable: false });
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());
    expect(document.body.textContent).toContain("Teşekkür ederiz.");

    await act(async () => { await vi.advanceTimersByTimeAsync(7_000); });
    expect(document.body.textContent).not.toContain("Köfte");
    expect(document.body.textContent).toContain("Siparişiniz için hazırız.");
    vi.useRealTimers();
  });

  it("has no critical or serious axe violations on the active order screen", async () => {
    document.title = "ALKAROS müşteri ekranı";
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse(snapshot);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });

  it("recovers on its own after a server error on the first load", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) {
        calls += 1;
        return calls === 1 ? jsonResponse({ error: { code: "X", message: "Sunucu hatası." } }, 503) : jsonResponse(snapshot);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("Ekran bilgisi alınamadı");
    expect(document.body.textContent).not.toContain("saniye sonra");

    await tick(1_000);
    expect(document.body.textContent).toContain("S-042");
  });

  it("keeps polling an unapproved pairing through a 429 and asks for a new code once it expired", async () => {
    vi.useFakeTimers();
    let completions = 0;
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "Eşleştirme gerekli." } }, 401);
      if (path.endsWith("/pairing-requests")) return jsonResponse(pairingBody(created++ === 0 ? "AAAA1111" : "BBBB2222"));
      if (path.includes("/complete")) {
        completions += 1;
        if (completions === 1) return jsonResponse({ error: { code: "RATE_LIMITED", message: "Çok fazla istek." } }, 429);
        return jsonResponse({ error: { code: "PAIRING_EXPIRED", message: "Eşleştirme kodunun süresi doldu." } }, 410);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("AAAA1111");

    await tick(2_000);
    expect(document.body.textContent).toContain("AAAA1111");
    expect(document.body.textContent).not.toContain("Ekran bilgisi alınamadı");

    await tick(6_000);
    expect(document.body.textContent).toContain("BBBB2222");
  });

  it("states the pairing instruction with the till's own button name and the real code lifetime", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "Eşleştirme gerekli." } }, 401);
      if (path.endsWith("/pairing-requests")) return jsonResponse(pairingBody("CCCC3333"));
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("“Müşteri ekranını eşleştir” düğmesiyle açılan pencereye");
    expect(document.body.textContent).toContain("2 dakika sonra");
  });

  it("counts the code lifetime from the server's seconds, not from a clock that is ten minutes ahead", async () => {
    vi.useFakeTimers();
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        return jsonResponse({ ...pairingBody("DDDD4444"), expiresAt: new Date(Date.now() - 480_000).toISOString() });
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    await tick(60_000);
    expect(created).toBe(1);
    expect(document.body.textContent).toContain("2 dakika sonra");

    await tick(60_000);
    expect(created).toBe(2);
  });

  it("tells a rate-limited display how long it waits, counts down and asks again by itself", async () => {
    vi.useFakeTimers();
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        if (created === 1) {
          return new Response(JSON.stringify({ error: { code: "RATE_LIMITED", message: "RAW-SERVER-TEXT" } }), {
            status: 429, headers: { "Content-Type": "application/json", "Retry-After": "7" },
          });
        }
        return jsonResponse(pairingBody("EEEE5555"));
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("Kısa sürede çok fazla deneme yapıldı");
    expect(document.body.textContent).toContain("7 saniye");
    expect(document.body.textContent).not.toContain("RAW-SERVER-TEXT");

    await tick(1_000);
    expect(document.body.textContent).toContain("6 saniye");

    for (let second = 0; second < 6; second += 1) await tick(1_000);
    expect(created).toBe(2);
    expect(document.body.textContent).toContain("EEEE5555");
  });

  it("waits out a rate-limited snapshot request instead of hammering the server", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (!path.includes("/snapshot")) throw new Error(`unexpected fetch: ${path}`);
      calls += 1;
      if (calls === 1) {
        return new Response(JSON.stringify({ error: { code: "RATE_LIMITED", message: "RAW-SERVER-TEXT" } }), {
          status: 429, headers: { "Content-Type": "application/json", "Retry-After": "5" },
        });
      }
      return jsonResponse(snapshot);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("5 saniye");

    for (let second = 0; second < 4; second += 1) await tick(1_000);
    expect(calls).toBe(1);
    await tick(1_000);
    expect(calls).toBe(2);
    expect(document.body.textContent).toContain("S-042");
  });

  it("retries what failed when the retry button is pressed: a new code after a failed code request", async () => {
    vi.useFakeTimers();
    const paths: string[] = [];
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      paths.push(path);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        return created === 1 ? jsonResponse({ error: { code: "X", message: "x" } }, 503) : jsonResponse(pairingBody("FFFF6666"));
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    const snapshotsBefore = paths.filter((path) => path.includes("/snapshot")).length;
    await act(async () => { (document.querySelector(".display-retry") as HTMLButtonElement).click(); });
    await tick(0);

    expect(paths.filter((path) => path.includes("/snapshot")).length).toBe(snapshotsBefore);
    expect(document.body.textContent).toContain("FFFF6666");
  });

  it("retries what failed when the retry button is pressed: the screen request after a failed first load", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (!String(input).includes("/snapshot")) throw new Error(`unexpected fetch: ${String(input)}`);
      calls += 1;
      return calls === 1 ? jsonResponse({ error: { code: "X", message: "x" } }, 503) : jsonResponse(snapshot);
    }));

    await render();
    await tick(0);
    await act(async () => { (document.querySelector(".display-retry") as HTMLButtonElement).click(); });
    await tick(0);

    expect(calls).toBe(2);
    expect(document.body.textContent).toContain("S-042");
  });

  it("stops polling a ready screen while the server asks it to wait", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (!path.includes("/snapshot")) throw new Error(`unexpected fetch: ${path}`);
      calls += 1;
      if (calls === 2) {
        return new Response(JSON.stringify({ error: { code: "RATE_LIMITED", message: "x" } }), {
          status: 429, headers: { "Content-Type": "application/json", "Retry-After": "12" },
        });
      }
      return jsonResponse(snapshot);
    }));

    await render();
    await tick(0);
    await tick(5_000);
    expect(calls).toBe(2);
    await tick(5_000);
    expect(calls).toBe(2);
    await act(async () => { hub.changed?.(); });
    expect(calls).toBe(2);
    await tick(10_000);
    expect(calls).toBe(3);
    expect(document.body.textContent).toContain("S-042");
  });

  it("keeps a ready screen quiet for five seconds after a 429 without Retry-After, whatever wakes it", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (!String(input).includes("/snapshot")) throw new Error(`unexpected fetch: ${String(input)}`);
      calls += 1;
      return calls === 2 ? jsonResponse({ error: { code: "RATE_LIMITED", message: "x" } }, 429) : jsonResponse(snapshot);
    }));

    await render();
    await tick(0);
    await tick(5_000);
    expect(calls).toBe(2);
    await act(async () => { hub.changed?.(); });
    await act(async () => { hub.reconnected?.(); });
    await act(async () => { window.dispatchEvent(new Event("online")); });
    expect(calls).toBe(2);
    await tick(5_000);
    expect(calls).toBe(3);
  });

  it("stops asking whether a pairing was approved while the server asks it to wait", async () => {
    vi.useFakeTimers();
    let completions = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) return jsonResponse(pairingBody("GGGG7777"));
      if (path.includes("/complete")) {
        completions += 1;
        return new Response(JSON.stringify({ error: { code: "RATE_LIMITED", message: "x" } }), {
          status: 429, headers: { "Content-Type": "application/json", "Retry-After": "10" },
        });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    await tick(2_000);
    expect(completions).toBe(1);
    await tick(6_000);
    expect(completions).toBe(1);
    await tick(4_000);
    expect(completions).toBe(2);
  });

  it("ignores a late answer about a code that was already replaced", async () => {
    vi.useFakeTimers();
    let created = 0;
    let late: ((response: Response) => void) | undefined;
    vi.stubGlobal("fetch", vi.fn((input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return Promise.resolve(jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401));
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        return Promise.resolve(jsonResponse(pairingBody(created === 1 ? "IIII9999" : "JJJJ1010")));
      }
      if (path.includes("/complete")) {
        return created === 1 && !late
          ? new Promise<Response>((resolve) => { late = resolve; })
          : Promise.resolve(jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409));
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    await tick(2_000);
    expect(late).toBeDefined();
    await tick(118_000);
    expect(created).toBe(2);

    await act(async () => { late!(jsonResponse({ error: { code: "PAIRING_EXPIRED", message: "x" } }, 410)); });
    await tick(0);

    expect(created).toBe(2);
    expect(document.body.textContent).toContain("JJJJ1010");
  });

  it("does not loop on new codes when an older Host sends no lifetime", async () => {
    vi.useFakeTimers();
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        const { expiresInSeconds: _lifetime, ...withoutLifetime } = pairingBody("HHHH8888");
        return jsonResponse(withoutLifetime);
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    await tick(60_000);

    expect(created).toBe(1);
    expect(document.body.textContent).toContain("2 dakika sonra");
  });

  it("shows the code as ready once the cashier approved it", async () => {
    vi.useFakeTimers();
    let approved = false;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return approved ? jsonResponse(snapshot) : jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) return jsonResponse(pairingBody("KKKK1212"));
      if (path.includes("/complete")) {
        approved = true;
        return jsonResponse({ displayId: "d-1" });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("KKKK1212");
    await tick(2_000);

    expect(document.body.textContent).toContain("S-042");
    expect(document.body.textContent).not.toContain("KKKK1212");
  });

  it("waits five seconds before asking whether a pairing was approved when a 429 names no Retry-After", async () => {
    vi.useFakeTimers();
    let completions = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) return jsonResponse(pairingBody("LLLL3434"));
      if (path.includes("/complete")) {
        completions += 1;
        return jsonResponse({ error: { code: "RATE_LIMITED", message: "x" } }, 429);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    await tick(2_000);
    expect(completions).toBe(1);
    await tick(2_000);
    await tick(2_000);
    expect(completions).toBe(1);
    await tick(2_000);
    expect(completions).toBe(2);
  });

  it("counts down five seconds after a first load answered 429 without Retry-After", async () => {
    vi.useFakeTimers();
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      if (!String(input).includes("/snapshot")) throw new Error(`unexpected fetch: ${String(input)}`);
      calls += 1;
      return calls === 1 ? jsonResponse({ error: { code: "RATE_LIMITED", message: "x" } }, 429) : jsonResponse(snapshot);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain("5 saniye");
    for (let second = 0; second < 4; second += 1) await tick(1_000);
    expect(calls).toBe(1);
    await tick(1_000);
    expect(calls).toBe(2);
  });

  it.each([
    [90, 89, 90, "2 dakika sonra"],
    [300, 299, 300, "5 dakika sonra"],
    [30, 29, 30, "1 dakika sonra"],
    [0, 119, 120, "2 dakika sonra"],
    [-5, 119, 120, "2 dakika sonra"],
    [Number.NaN, 119, 120, "2 dakika sonra"],
  ])("with a code lifetime of %d seconds asks for a new code at %d..%d seconds and says %s", async (lifetime, before, at, sentence) => {
    vi.useFakeTimers();
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        return jsonResponse({ ...pairingBody("MMMM5656"), expiresInSeconds: lifetime });
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(document.body.textContent).toContain(sentence);
    await tick(before * 1_000);
    expect(created).toBe(1);
    await tick((at - before) * 1_000);
    expect(created).toBe(2);
  });

  it("backs off 1, 2 and 4 seconds on repeated failures and starts again at 1 second after a success", async () => {
    vi.useFakeTimers();
    let created = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "x" } }, 401);
      if (path.endsWith("/pairing-requests")) {
        created += 1;
        return created <= 3 || created === 5 ? jsonResponse({ error: { code: "X", message: "x" } }, 503) : jsonResponse(pairingBody("NNNN7878"));
      }
      if (path.includes("/complete")) return jsonResponse({ error: { code: "PAIRING_NOT_APPROVED", message: "x" } }, 409);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    expect(created).toBe(1);
    await tick(999);
    expect(created).toBe(1);
    await tick(1);
    expect(created).toBe(2);
    await tick(1_000);
    expect(created).toBe(2);
    await tick(999);
    expect(created).toBe(2);
    await tick(1);
    expect(created).toBe(3);
    for (let second = 0; second < 3; second += 1) await tick(1_000);
    expect(created).toBe(3);
    await tick(999);
    expect(created).toBe(3);
    await tick(1);
    expect(created).toBe(4);

    await tick(120_000);
    expect(created).toBe(5);
    await tick(999);
    expect(created).toBe(5);
    await tick(1);
    expect(created).toBe(6);
  });

  it.each([
    [500, "Sunucu şu an yanıt veremiyor"],
    [403, "Kasiyerden yardım isteyin"],
    [0, "Sunucuya ulaşılamadı"],
  ])("never prints the server's own wording for a %i answer and counts nothing down", async (status, sentence) => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) {
        if (status === 0) throw new TypeError("RAW-SERVER-TEXT");
        return jsonResponse({ error: { code: "X", message: "RAW-SERVER-TEXT" } }, status);
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain(sentence);
    expect(document.body.textContent).not.toContain("RAW-SERVER-TEXT");
    expect(document.body.textContent).not.toContain("saniye sonra");
  });

  it("drops a slow older snapshot that arrives after a newer one", async () => {
    const answers: Array<(response: Response) => void> = [];
    vi.stubGlobal("fetch", vi.fn((input: RequestInfo | URL) => {
      const path = String(input);
      if (!path.includes("/snapshot")) throw new Error(`unexpected fetch: ${path}`);
      return new Promise<Response>((resolve) => answers.push(resolve));
    }));

    await render();
    await act(async () => Promise.resolve());
    expect(answers).toHaveLength(1);
    await act(async () => { answers[0](jsonResponse(snapshot)); });
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    await act(async () => { hub.changed?.(); });
    await act(async () => { hub.changed?.(); });
    expect(answers).toHaveLength(3);
    await act(async () => { answers[2](jsonResponse({ ...snapshot, revision: 5, orderNumber: "S-NEW" })); });
    await act(async () => { answers[1](jsonResponse({ ...snapshot, revision: 4, orderNumber: "S-OLD" })); });

    expect(document.body.textContent).toContain("S-NEW");
    expect(document.body.textContent).not.toContain("S-OLD");
  });

  it("starts the live connection again after it closes, and refreshes when it returns", async () => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse(snapshot);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await tick(0);
    const before = startMock.mock.calls.length;

    await act(async () => { hub.closed?.(); });
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 1);

    startMock.mockRejectedValueOnce(new Error("down"));
    await act(async () => { hub.closed?.(); });
    await tick(2_000);
    await tick(2_000);
    expect(startMock.mock.calls.length).toBeGreaterThanOrEqual(before + 3);

    const fetchMock = globalThis.fetch as unknown as ReturnType<typeof vi.fn>;
    const fetchesBefore = fetchMock.mock.calls.length;
    await act(async () => { hub.reconnected?.(); });
    expect(fetchMock.mock.calls.length).toBe(fetchesBefore + 1);
  });

  async function showSnapshot(extra: Record<string, unknown>) {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, ...extra });
      throw new Error(`unexpected fetch: ${path}`);
    }));
    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());
  }

  const statementRows = () =>
    Array.from(document.querySelectorAll(".display-total-breakdown > div")).map((row) => ({
      label: row.children[0].textContent,
      value: row.children[1].textContent,
    }));

  const money = (value: number) => formatMoney(value, "TRY");

  it("lists the statement in the order a customer reads it: subtotal, discount, tip, fee, bill total, paid, VAT", async () => {
    await showSnapshot({
      lines: [
        { itemId: "i-1", name: "Köfte", quantity: 2, unitPrice: 280, lineTotal: 560, discountAmount: 0 },
        { itemId: "i-2", name: "Ayran", quantity: 4, unitPrice: 100, lineTotal: 400, discountAmount: 40 },
      ],
      subtotal: 960, discountTotal: 160, tipTotal: 50, feeTotal: 20, total: 870, paidTotal: 400, remainingTotal: 470,
    });

    expect(statementRows()).toEqual([
      { label: "Ara toplam", value: money(960) },
      { label: "İndirim", value: `−${money(160)}` },
      { label: "Bahşiş", value: `+${money(50)}` },
      { label: "Ücret", value: `+${money(20)}` },
      { label: "Hesap toplamı", value: money(870) },
      { label: "Ödenen", value: `−${money(400)}` },
      { label: "İçindeki KDV", value: money(50.91) },
    ]);
    const card = document.querySelector(".display-total-card")!;
    expect(card.querySelector(".display-kicker")!.textContent).toBe("KALAN TUTAR");
    expect(card.querySelector(".display-total-figure")!.textContent).toBe(money(470));
  });

  it("shows only the rows that exist: no tip, fee, bill total or paid row on a plain unpaid order", async () => {
    await showSnapshot({ tipTotal: 0, feeTotal: 0, paidTotal: 0, remainingTotal: null });

    expect(statementRows().map((row) => row.label)).toEqual(["Ara toplam", "İçindeki KDV"]);
    expect(document.querySelector(".display-total-card .display-kicker")!.textContent).toBe("ÖDENECEK TOPLAM");
  });

  it("shows a bill total once a tip alone separates the subtotal from what is due", async () => {
    await showSnapshot({ tipTotal: 30, total: 590 });

    expect(statementRows().map((row) => row.label)).toEqual(["Ara toplam", "Bahşiş", "Hesap toplamı", "İçindeki KDV"]);
  });

  it("marks a discounted line with its discount and a complimentary line as an offer, showing what is charged and the price it replaces", async () => {
    await showSnapshot({
      lines: [
        { itemId: "i-1", name: "Köfte", quantity: 2, unitPrice: 280, lineTotal: 560, discountAmount: 0 },
        { itemId: "i-2", name: "Ayran", quantity: 4, unitPrice: 100, lineTotal: 400, discountAmount: 40 },
        { itemId: "i-3", name: "Künefe", quantity: 1, unitPrice: 150, lineTotal: 150, discountAmount: 150 },
      ],
      subtotal: 1110, discountTotal: 190, total: 920,
    });

    const amount = (line: Element) => line.querySelector(".display-line-amount strong")!.textContent;
    const struck = (line: Element) => line.querySelector(".display-line-amount s")?.textContent;
    const lines = Array.from(document.querySelectorAll(".display-line"));
    expect(lines[0].textContent).not.toContain("İndirim");
    expect(lines[0].textContent).not.toContain("İkram");
    expect(amount(lines[0])).toBe(money(560));
    expect(struck(lines[0])).toBeUndefined();
    expect(lines[1].textContent).toContain(`İndirim −${money(40)}`);
    expect(amount(lines[1])).toBe(money(360));
    expect(struck(lines[1])).toBe(money(400));
    expect(lines[2].textContent).toContain("İkram");
    expect(lines[2].textContent).not.toContain("İndirim");
    expect(amount(lines[2])).toBe(money(0));
    expect(struck(lines[2])).toBe(money(150));
  });

  it("no longer prints the technical live-update and revision label", async () => {
    await showSnapshot({});

    expect(document.body.textContent).not.toContain("Canlı güncelleme");
    expect(document.body.textContent).not.toContain("v3");
  });

  it("tells a customer who has paid in full how much was paid", async () => {
    await showSnapshot({ state: "Completed", total: 870, paidTotal: 870, remainingTotal: 0, message: "Ödemeniz alındı." });

    const screen = document.querySelector(".completed-screen")!;
    expect(screen.textContent).toContain("Teşekkür ederiz.");
    expect(screen.querySelector(".display-paid-total")!.textContent).toBe(`Ödenen toplam${money(870)}`);
  });

  it("shows no paid amount on a completed order that has no payment on record", async () => {
    await showSnapshot({ state: "Completed", paidTotal: 0 });

    expect(document.querySelector(".completed-screen")!.textContent).not.toContain("Ödenen toplam");
  });

  describe("long orders", () => {
    const manyLines = Array.from({ length: 12 }, (_, index) => (
      { itemId: `i-${index}`, name: `Ürün ${index + 1}`, quantity: 1, unitPrice: 10, lineTotal: 10, discountAmount: 0 }
    ));
    const layout = { clientHeight: 500, scrollHeight: 1_200, lineHeight: 100 };
    const scrollTops = new WeakMap<Element, number>();
    let scrollTo: ReturnType<typeof vi.fn>;

    const stubLayout = (reducedMotion = false) => {
      const proto = HTMLElement.prototype;
      const isList = (element: Element) => element.classList.contains("display-lines-scroll");
      Object.defineProperty(proto, "clientHeight", { configurable: true, get() { return isList(this) ? layout.clientHeight : 0; } });
      Object.defineProperty(proto, "scrollHeight", { configurable: true, get() { return isList(this) ? layout.scrollHeight : 0; } });
      Object.defineProperty(proto, "scrollTop", { configurable: true, get() { return scrollTops.get(this) ?? 0; }, set(value: number) { scrollTops.set(this, value); } });
      Object.defineProperty(proto, "offsetHeight", { configurable: true, get() { return this.classList.contains("display-line") ? layout.lineHeight : 0; } });
      Object.defineProperty(proto, "offsetTop", {
        configurable: true,
        get() { return this.classList.contains("display-line") ? Array.from(this.parentElement!.children).indexOf(this) * layout.lineHeight : 0; },
      });
      scrollTo = vi.fn(function (this: HTMLElement, options: ScrollToOptions) {
        scrollTops.set(this, options.top ?? 0);
        this.dispatchEvent(new Event("scroll"));
      });
      proto.scrollTo = scrollTo as unknown as typeof proto.scrollTo;
      vi.stubGlobal("matchMedia", (query: string) => ({ matches: reducedMotion && query.includes("reduce"), media: query }));
    };

    afterEach(() => {
      for (const property of ["clientHeight", "scrollHeight", "scrollTop", "offsetHeight", "offsetTop"]) {
        delete (HTMLElement.prototype as unknown as Record<string, unknown>)[property];
      }
      delete (HTMLElement.prototype as unknown as Record<string, unknown>).scrollTo;
    });

    it("says how many items are out of sight when the list is longer than the screen", async () => {
      stubLayout();
      await showSnapshot({ lines: manyLines });

      expect(document.querySelector(".display-more-lines")!.textContent).toBe("+7 kalem daha");
    });

    it("shows no indicator when every item fits", async () => {
      stubLayout();
      layout.scrollHeight = 500;
      try {
        await showSnapshot({ lines: manyLines.slice(0, 4) });
      } finally {
        layout.scrollHeight = 1_200;
      }

      expect(document.querySelector(".display-more-lines")).toBeNull();
    });

    it("pages through a long list on its own and starts over at the end", async () => {
      vi.useFakeTimers();
      stubLayout();
      await showSnapshot({ lines: manyLines });
      await tick(0);

      await tick(6_000);
      expect(scrollTo).toHaveBeenLastCalledWith({ top: 500, behavior: "smooth" });
      expect(document.querySelector(".display-more-lines")!.textContent).toBe("+2 kalem daha");

      await tick(6_000);
      expect(scrollTo).toHaveBeenLastCalledWith({ top: 700, behavior: "smooth" });
      expect(document.querySelector(".display-more-lines")).toBeNull();

      await tick(6_000);
      expect(scrollTo).toHaveBeenLastCalledWith({ top: 0, behavior: "smooth" });
      expect(document.querySelector(".display-more-lines")!.textContent).toBe("+7 kalem daha");
    });

    it("starts each page at the first item that was cut off, whatever the item height", async () => {
      vi.useFakeTimers();
      stubLayout();
      layout.lineHeight = 120;
      layout.scrollHeight = 1_440;
      try {
        await showSnapshot({ lines: manyLines });
        await tick(6_000);
        expect(scrollTo).toHaveBeenLastCalledWith({ top: 480, behavior: "smooth" });
      } finally {
        layout.lineHeight = 100;
        layout.scrollHeight = 1_200;
      }
    });

    it("moves on past an item taller than the list instead of sticking to it", async () => {
      vi.useFakeTimers();
      stubLayout();
      layout.lineHeight = 600;
      layout.scrollHeight = 7_200;
      try {
        await showSnapshot({ lines: manyLines });
        await tick(6_000);
        expect(scrollTo).toHaveBeenLastCalledWith({ top: 500, behavior: "smooth" });
      } finally {
        layout.lineHeight = 100;
        layout.scrollHeight = 1_200;
      }
    });

    it("turns the page without animation when the viewer asked for reduced motion", async () => {
      vi.useFakeTimers();
      stubLayout(true);
      await showSnapshot({ lines: manyLines });
      await tick(6_000);

      expect(scrollTo).toHaveBeenLastCalledWith({ top: 500, behavior: "auto" });
    });

    it("measures again when the list box changes size, and stops watching and paging once the screen is gone", async () => {
      vi.useFakeTimers();
      stubLayout();
      await showSnapshot({ lines: manyLines });
      expect(document.querySelector(".display-more-lines")!.textContent).toBe("+7 kalem daha");

      layout.clientHeight = 700;
      try {
        await act(async () => { resizeObservers.forEach((callback) => callback()); });
        expect(document.querySelector(".display-more-lines")!.textContent).toBe("+5 kalem daha");

        await act(async () => root!.unmount());
        root = null;
        expect(resizeObservers.size).toBe(0);
        await tick(30_000);
        expect(scrollTo).not.toHaveBeenCalled();
      } finally {
        layout.clientHeight = 500;
      }
    });

    it("does not scroll a list that fits", async () => {
      vi.useFakeTimers();
      stubLayout();
      layout.scrollHeight = 500;
      try {
        await showSnapshot({ lines: manyLines.slice(0, 4) });
        await tick(30_000);
      } finally {
        layout.scrollHeight = 1_200;
      }

      expect(scrollTo).not.toHaveBeenCalled();
    });
  });

  describe("idle screen and freshness", () => {
    const settle = async () => {
      for (let round = 0; round < 4; round += 1) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    };

    it("shows the business's own picture on an idle screen, and a video when that is what it uploaded", async () => {
      const createObjectURL = vi.fn(() => "blob:idle");
      (URL as unknown as { createObjectURL: unknown }).createObjectURL = createObjectURL;
      (URL as unknown as { revokeObjectURL: unknown }).revokeObjectURL = vi.fn();
      let contentType = "image/png";
      vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
        const path = String(input);
        if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, state: "Idle", lines: [], orderId: null });
        if (path.includes("/screensaver")) return new Response("x", { status: 200, headers: { "Content-Type": contentType } });
        throw new Error(`unexpected fetch: ${path}`);
      }));

      await render();
      await settle();
      expect(document.querySelector("img.idle-screensaver-image")!.getAttribute("src")).toBe("blob:idle");
      expect(document.querySelector("video")).toBeNull();

      await act(async () => root!.unmount());
      root = null;
      contentType = "video/mp4";
      await render();
      await settle();
      expect(document.querySelector("video.idle-screensaver-image")!.getAttribute("src")).toBe("blob:idle");
    });

    it("falls back to the welcome card when no picture is set", async () => {
      vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
        const path = String(input);
        if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, state: "Idle", lines: [], orderId: null });
        if (path.includes("/screensaver")) return new Response("", { status: 404 });
        throw new Error(`unexpected fetch: ${path}`);
      }));

      await render();
      await act(async () => Promise.resolve());
      await act(async () => Promise.resolve());

      expect(document.querySelector(".idle-screensaver-image")).toBeNull();
      expect(document.body.textContent).toContain("Siparişiniz için hazırız.");
    });

    it("keeps the last amounts for nine seconds without an answer and removes them at ten", async () => {
      vi.useFakeTimers();
      let calls = 0;
      vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
        const path = String(input);
        if (!path.includes("/snapshot")) throw new Error(`unexpected fetch: ${path}`);
        calls += 1;
        return calls === 1 ? jsonResponse(snapshot) : jsonResponse({ error: { code: "X", message: "x" } }, 503);
      }));

      await render();
      await tick(0);
      await tick(5_000);
      await tick(4_000);
      expect(document.body.textContent).toContain("S-042");
      expect(document.querySelector(".connection-banner")).not.toBeNull();

      await tick(1_000);
      expect(document.body.textContent).not.toContain("S-042");
      expect(document.body.textContent).toContain("Bilgi güncellenemiyor");
    });
  });

  describe("stylesheet", () => {
    const displayStyles = readFileSync(join(process.cwd(), "src", "styles.css"), "utf8").split(String.fromCharCode(13)).join("");
    const rule = (selector: string) => {
      const start = displayStyles.indexOf(`\n${selector} {`);
      if (start < 0) throw new Error(`no rule for ${selector}`);
      return displayStyles.slice(start, displayStyles.indexOf("}", start));
    };
    const declaration = (selector: string, property: string) => {
      const match = new RegExp(`[{;\\s]${property}:\\s*([^;]+)`).exec(rule(selector));
      if (!match) throw new Error(`${selector} has no ${property}`);
      return match[1].trim();
    };
    // Smallest size the rule can produce: the first argument of clamp(), or the plain value.
    const smallestFontPx = (selector: string) => {
      const value = declaration(selector, "font-size");
      const first = /^clamp\(\s*([\d.]+)(rem|px)/.exec(value) ?? /^([\d.]+)(rem|px)/.exec(value);
      if (!first) throw new Error(`cannot read ${value}`);
      return Number(first[1]) * (first[2] === "rem" ? 16 : 1);
    };
    const rgbOf = (hex: string) => [0, 1, 2].map((index) => parseInt(hex.slice(1 + index * 2, 3 + index * 2), 16) / 255);
    const luminance = (rgb: number[]) => {
      const [r, g, b] = rgb.map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const contrast = (foreground: number[], background: number[]) => {
      const [light, dark] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
      return (light + 0.05) / (dark + 0.05);
    };

    it("sets item names, amounts and prices in type a customer can read from the other side of a counter", () => {
      expect(smallestFontPx(".display-line strong")).toBeGreaterThanOrEqual(28);
      expect(smallestFontPx(".display-line small")).toBeGreaterThanOrEqual(16);
      expect(smallestFontPx(".display-quantity")).toBeGreaterThanOrEqual(28);
    });

    it("fits the active screen inside the window: one column that fills the height, the connection notice in the flow", () => {
      expect(declaration(".active-display", "height")).toMatch(/^100d?vh$/);
      expect(declaration(".active-display", "display")).toBe("flex");
      expect(declaration(".display-order-grid", "flex")).toBe("1");
      expect(declaration(".display-order-grid", "min-height")).toBe("0");
      expect(declaration(".active-display .connection-banner", "position")).toBe("static");
    });

    it("lets the short-screen margin of the connection notice win over the base margin", () => {
      expect(displayStyles.lastIndexOf("\n  .active-display .connection-banner")).toBeGreaterThan(displayStyles.lastIndexOf("\n.active-display .connection-banner"));
    });

    it("keeps the totals card on screen at 1024 x 600 by letting only the item list scroll", () => {
      expect(declaration(".display-lines-scroll", "overflow-y")).toBe("auto");
      expect(declaration(".display-lines-scroll", "scrollbar-width")).toBe("none");
      expect(declaration(".display-lines-card", "min-height")).toBe("0");
      expect(declaration(".display-total-card", "overflow-y")).toBe("auto");
    });

    it.each([
      [".display-table-head", "#111b25"],
      [".display-order-number span", "#111b25"],
      [".display-line small", "#1a232c"],
      [".display-center-card small", "#0d151d"],
      [".display-total-breakdown", "#f5f2ed"],
    ])("keeps %s legible on its background (contrast 4.5:1)", (selector, background) => {
      expect(contrast(rgbOf(declaration(selector, "color")), rgbOf(background))).toBeGreaterThanOrEqual(4.5);
    });

    it("keeps the brand badge legible on a screensaver picture of any lightness, worst case a white one", () => {
      const alpha = Number(/rgba\(13, 21, 29, ([\d.]+)\)/.exec(declaration(".idle-screen-with-image .display-brand", "background"))![1]);
      const overWhite = [13, 21, 29].map((value) => (value * alpha + 255 * (1 - alpha)) / 255);
      expect(contrast(rgbOf("#f8fafb"), overWhite)).toBeGreaterThanOrEqual(4.5);
    });
  });
});
