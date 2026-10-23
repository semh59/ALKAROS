// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePendingChecksAlert } from "./usePendingChecksAlert";

const chime = vi.hoisted(() => vi.fn());
vi.mock("../../audioAlerts", () => ({ playNewItemChime: chime }));

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const listOf = (...ids: string[]) =>
  new Response(JSON.stringify(ids.map((orderId) => ({ orderId }))), { status: 200, headers: { "Content-Type": "application/json" } });

function Probe({ enabled, fetcher }: { enabled: boolean; fetcher: typeof fetch }) {
  const count = usePendingChecksAlert("t1", enabled, fetcher);
  return <output>{count}</output>;
}

describe("usePendingChecksAlert", () => {
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
  const shown = () => document.querySelector("output")?.textContent;

  it("counts the waiting checks, stays quiet on the first read and chimes once for a check it has not seen", async () => {
    const answers = [listOf("a"), listOf("a"), listOf("a", "b"), listOf("a", "b")];
    const fetcher = vi.fn(async () => answers.shift()!) as unknown as typeof fetch;
    await mount(true, fetcher);
    expect(shown()).toBe("1");
    expect(chime).not.toHaveBeenCalled();
    await tick();
    expect(chime).not.toHaveBeenCalled();
    await tick();
    expect(shown()).toBe("2");
    expect(chime).toHaveBeenCalledTimes(1);
    await tick();
    expect(chime).toHaveBeenCalledTimes(1);
  });

  it("does nothing while disabled and keeps the last count when a poll fails", async () => {
    const idle = vi.fn() as unknown as typeof fetch;
    await mount(false, idle);
    expect(idle).not.toHaveBeenCalled();

    await act(async () => root!.unmount());
    const answers = [listOf("a", "b")];
    const flaky = vi.fn(async () => { const next = answers.shift(); if (!next) throw new Error("offline"); return next; }) as unknown as typeof fetch;
    await mount(true, flaky);
    await tick();
    expect(shown()).toBe("2");
  });
});
