import { describe, expect, it, vi } from "vitest";
import { installTracePropagation, traceIdFrom, type TraceTarget } from "./tracePropagation";

function setup() {
  const calls: { input: RequestInfo | URL; headers: Headers; init?: RequestInit }[] = [];
  const target: TraceTarget = {
    location: { origin: "https://pos.test" },
    fetch: vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      calls.push({ input, headers: new Headers(init?.headers), init });
      return Promise.resolve(new Response(null, { status: 204 }));
    }) as unknown as typeof fetch,
  };
  installTracePropagation(target);
  return { target, calls };
}

describe("trace propagation", () => {
  it("adds a traceparent whose trace id is the existing correlation id without dashes", async () => {
    const { target, calls } = setup();
    const id = "3f2c1b0a-1111-4222-8333-444455556666";

    await target.fetch("/api/v1/orders", { headers: { "X-Correlation-Id": id } });

    expect(calls[0]!.headers.get("traceparent")).toMatch(/^00-3f2c1b0a111142228333444455556666-[0-9a-f]{16}-01$/);
    expect(calls[0]!.headers.get("X-Correlation-Id")).toBe(id);
  });

  it("creates a trace id and a matching correlation id when the call has none", async () => {
    const { target, calls } = setup();

    await target.fetch("/api/v1/orders");

    const traceparent = calls[0]!.headers.get("traceparent")!;
    const traceId = traceparent.split("-")[1];
    expect(traceId).toMatch(/^[0-9a-f]{32}$/);
    expect(calls[0]!.headers.get("X-Correlation-Id")).toBe(traceId);
  });

  it("leaves other origins, non-API paths and existing traceparents alone", async () => {
    const { target, calls } = setup();

    await target.fetch("https://elsewhere.test/api/v1/x");
    await target.fetch("/static/app.js");
    await target.fetch("/api/v1/x", { headers: { traceparent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01" } });

    expect(calls[0]!.headers.has("traceparent")).toBe(false);
    expect(calls[1]!.headers.has("traceparent")).toBe(false);
    expect(calls[2]!.headers.get("traceparent")).toBe("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
  });

  it("falls back to a random trace id for a correlation id that is not a UUID", () => {
    expect(traceIdFrom("not-a-uuid")).toMatch(/^[0-9a-f]{32}$/);
    expect(traceIdFrom("00000000-0000-0000-0000-000000000000")).not.toBe("0".repeat(32));
  });

  it("keeps the request body and method", async () => {
    const { target, calls } = setup();

    await target.fetch("/api/v1/x", { method: "POST", body: "{}" });

    expect(calls[0]!.init?.method).toBe("POST");
    expect(calls[0]!.init?.body).toBe("{}");
  });
});
