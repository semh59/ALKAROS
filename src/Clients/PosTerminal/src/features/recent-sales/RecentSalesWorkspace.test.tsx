// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RecentSalesWorkspace } from "./RecentSalesWorkspace";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const sale = (over: Record<string, unknown> = {}) => ({
  billId: "b-1", tableNumber: "5", closedAt: "2026-09-24T11:32:00Z", amount: 540, methods: ["Cash"], ...over,
});
const answer = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

describe("RecentSalesWorkspace", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();

  beforeEach(() => {
    document.body.innerHTML = '<div id="root"></div>';
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render(navigateTo = vi.fn()) {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<RecentSalesWorkspace terminalId="t-1" navigateTo={navigateTo} />); });
    return navigateTo;
  }
  const text = () => document.body.textContent ?? "";

  it("lists each sale with its place, closing time, amount and every payment method in Turkish", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: true, sales: [
      sale(),
      sale({ billId: "b-2", tableNumber: null, methods: ["Card", "Eft", "Account", "Voucher"] }),
    ] }));
    await render();

    const rows = [...document.querySelectorAll(".recent-sales__row")].map((row) => row.textContent ?? "");
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/terminals/t-1/billing/recent-sales");
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Masa 5 · 14:32");
    expect(rows[0]).toContain("Nakit");
    expect(rows[0]).toMatch(/540,00/);
    expect(rows[1]).toContain("Hızlı satış · 14:32");
    expect(rows[1]).toContain("Kart, Havale, Müşteri hesabı, Bilinmiyor");
    expect(text()).not.toMatch(/Card|Eft|Cash|Account|Voucher/);
  });

  it("opens the payment page with the refund section open when a sale is tapped", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: true, sales: [sale({ billId: "b 1" })] }));
    const navigateTo = await render();

    await act(async () => { (document.querySelector(".recent-sales__row") as HTMLButtonElement).click(); });

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b%201&open=refund&from=recent-sales");
  });

  it("says there is nothing yet when the session has no closed sale", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: true, sales: [] }));
    await render();

    expect(text()).toContain("Bu oturumda kapanmış satış yok");
  });

  it("asks the cashier to open the till first when there is no open session", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: false, sales: [] }));
    await render();

    expect(text()).toContain("Son satışları görmek için önce kasayı açın");
    expect(text()).not.toContain("Bu oturumda kapanmış satış yok");
  });

  it("shows a loading sentence until the answer arrives", async () => {
    fetchMock.mockReturnValue(new Promise(() => undefined));
    await render();

    expect(text()).toContain("Son satışlar yükleniyor");
  });

  it.each([
    [403, "Bu listeyi görmek için yetkiniz yok."],
    [500, "Son satışlar okunamadı."],
  ])("shows a fixed Turkish sentence for status %i and never the server body", async (status, sentence) => {
    fetchMock.mockResolvedValue(answer({ error: { code: "BOOM", message: "SqlException: secret" } }, status));
    await render();

    expect(document.querySelector("[role='alert']")?.textContent).toBe(sentence);
    expect(text()).not.toContain("SqlException");
  });

  it("keeps the old list with a stale warning when a refresh fails, and recovers on the next success", async () => {
    fetchMock.mockResolvedValueOnce(answer({ hasOpenSession: true, sales: [sale()] }));
    await render();
    fetchMock.mockRejectedValueOnce(new Error("offline"));

    await act(async () => { (document.querySelector(".recent-sales__refresh") as HTMLButtonElement).click(); });

    expect(document.querySelectorAll(".recent-sales__row")).toHaveLength(1);
    expect(document.querySelector(".recent-sales__stale")?.textContent).toContain("Aşağıdaki liste eski olabilir");
    expect(document.querySelector("[role='alert']")?.textContent).toBe("Sunucuya ulaşılamadı. Son satışlar okunamadı.");

    fetchMock.mockResolvedValueOnce(answer({ hasOpenSession: true, sales: [sale(), sale({ billId: "b-2" })] }));
    await act(async () => { (document.querySelector(".recent-sales__refresh") as HTMLButtonElement).click(); });

    expect(document.querySelectorAll(".recent-sales__row")).toHaveLength(2);
    expect(document.querySelector(".recent-sales__stale")).toBeNull();
    expect(document.querySelector("[role='alert']")).toBeNull();
  });

  it("lets only the newest answer write the list when two reads overlap", async () => {
    let releaseSlow: (response: Response) => void = () => undefined;
    fetchMock.mockReturnValueOnce(new Promise<Response>((resolve) => { releaseSlow = resolve; }));
    await render();
    fetchMock.mockResolvedValueOnce(answer({ hasOpenSession: true, sales: [sale({ billId: "new" })] }));
    await act(async () => { (document.querySelector(".recent-sales__refresh") as HTMLButtonElement).click(); });

    await act(async () => { releaseSlow(answer({ hasOpenSession: true, sales: [sale({ billId: "old-1" }), sale({ billId: "old-2" })] })); });

    expect(document.querySelectorAll(".recent-sales__row")).toHaveLength(1);
  });

  it("reads again every minute and stops reading after it is closed", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      fetchMock.mockResolvedValue(answer({ hasOpenSession: true, sales: [] }));
      await render();
      expect(fetchMock).toHaveBeenCalledTimes(1);

      await act(async () => { vi.advanceTimersByTime(60_000); });
      expect(fetchMock).toHaveBeenCalledTimes(2);

      act(() => root!.unmount());
      root = null;
      await act(async () => { vi.advanceTimersByTime(120_000); });
      expect(fetchMock).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it("names the amount and methods in the row label so a screen reader hears what is shown", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: true, sales: [sale()] }));
    await render();

    expect(document.querySelector(".recent-sales__row")?.getAttribute("aria-label")).toMatch(/Masa 5 · 14:32, Nakit, .*540,00.*iade sayfasını aç/);
  });

  it("does not break on an answer without a sales list", async () => {
    fetchMock.mockResolvedValue(answer({ hasOpenSession: true }));
    await render();

    expect(text()).toContain("Bu oturumda kapanmış satış yok");
  });
});
