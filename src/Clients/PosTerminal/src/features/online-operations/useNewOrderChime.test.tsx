// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useNewOrderChime } from "./useNewOrderChime";

const chime = vi.hoisted(() => vi.fn());
vi.mock("../../audioAlerts", () => ({ playNewItemChime: chime }));

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const queueOf = (...ids: string[]) =>
  new Response(JSON.stringify({ orders: ids.map((orderId) => ({ orderId })), problems: [], retries: {} }), { status: 200, headers: { "Content-Type": "application/json" } });

function Probe({ enabled, fetcher }: { enabled: boolean; fetcher: typeof fetch }) {
  useNewOrderChime("t1", enabled, fetcher);
  return null;
}

describe("useNewOrderChime", () => {
  let root: Root | null = null;

  beforeEach(() => { vi.useFakeTimers(); chime.mockClear(); });
  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.useRealTimers();
  });

  async function mount(enabled: boolean, fetcher: typeof fetch) {
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<Probe enabled={enabled} fetcher={fetcher} />));
  }

  const tick = () => act(async () => { await vi.advanceTimersByTimeAsync(20_000); });

  it("stays quiet on the first read and on an unchanged queue, and chimes once for an order it has not seen", async () => {
    const answers = [queueOf("a"), queueOf("a"), queueOf("a", "b"), queueOf("a", "b")];
    const fetcher = vi.fn(async () => answers.shift()!) as unknown as typeof fetch;
    await mount(true, fetcher);
    expect(chime).not.toHaveBeenCalled();
    await tick();
    expect(chime).not.toHaveBeenCalled();
    await tick();
    expect(chime).toHaveBeenCalledTimes(1);
    await tick();
    expect(chime).toHaveBeenCalledTimes(1);
  });

  it("asks for every source and sends nothing while disabled", async () => {
    const fetcher = vi.fn(async () => queueOf()) as unknown as typeof fetch;
    await mount(false, fetcher);
    await tick();
    expect(fetcher).not.toHaveBeenCalled();

    await act(async () => root!.unmount());
    const enabledFetcher = vi.fn(async () => queueOf()) as unknown as typeof fetch;
    await mount(true, enabledFetcher);
    expect(String((vi.mocked(enabledFetcher).mock.calls[0] as unknown[])[0])).toBe("/api/v1/terminals/t1/online-operations?source=all");
  });

  it("keeps polling after a failed read and stops once the screen is left", async () => {
    const answers: (Response | Error)[] = [queueOf("a"), new Error("offline"), queueOf("a", "b")];
    const fetcher = vi.fn(async () => { const next = answers.shift()!; if (next instanceof Error) throw next; return next; }) as unknown as typeof fetch;
    await mount(true, fetcher);
    await tick();
    await tick();
    expect(chime).toHaveBeenCalledTimes(1);

    await act(async () => root!.unmount());
    root = null;
    const calls = vi.mocked(fetcher).mock.calls.length;
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });
    expect(vi.mocked(fetcher).mock.calls.length).toBe(calls);
  });
});
