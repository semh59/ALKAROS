// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

// V1-RMD-374 (module-by-module UI audit, 2026-09-27): mirrors
// Cashier.help-alerts.test.tsx's own SignalR mock - the reason no other
// hub usage in this codebase is unit-tested at a deeper layer than "does it
// call start()".
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
  lines: [{ itemId: "i-1", name: "Köfte", quantity: 2, lineTotal: 560 }],
  subtotal: 560, discountTotal: 0, taxTotal: 50.91, total: 560, currency: "TRY",
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
          ...snapshot, total: 260, paidTotal: 100, remainingTotal: 140, message: "Ödemenizin bir bölümü alındı; kalan tutar ödenecek.",
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

    expect(document.body.textContent).toContain("“Ekranı eşleştir” düğmesiyle açılan pencereye");
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
});
