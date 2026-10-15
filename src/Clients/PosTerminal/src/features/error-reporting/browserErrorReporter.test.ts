import { describe, expect, it, vi } from "vitest";
import { createBrowserErrorReporter } from "./browserErrorReporter";

describe("browser error reporter", () => {
  it("sends at most three reports for the same error", () => {
    const send = vi.fn(() => true);
    const reporter = createBrowserErrorReporter({ send, route: () => "/cashier/", newId: () => "id" });

    for (let i = 0; i < 50; i += 1) {
      reporter.report("js_error", "app.js:1:1");
    }

    expect(send).toHaveBeenCalledTimes(3);
  });

  it("sends only the id, the fixed kind and the route", async () => {
    let captured: Blob | undefined;
    const send = vi.fn((_url: string, data?: BodyInit | null) => {
      captured = data as Blob;
      return true;
    });
    const reporter = createBrowserErrorReporter({ send, route: () => "/cashier/orders", newId: () => "abc" });

    reporter.report("unhandled_rejection", "TypeError");

    const parsed = JSON.parse(await captured!.text()) as Record<string, string>;
    expect(Object.keys(parsed).sort()).toEqual(["eventId", "kind", "route"]);
    expect(parsed).toEqual({ eventId: "abc", kind: "unhandled_rejection", route: "/cashier/orders" });
    expect(send.mock.calls[0]?.[0]).toBe("/api/v1/telemetry/browser-errors");
  });

  it("stops after the total cap so a runaway page cannot flood", () => {
    const send = vi.fn(() => true);
    const reporter = createBrowserErrorReporter({ send, route: () => "/x", newId: () => "id" });

    for (let i = 0; i < 100; i += 1) {
      reporter.report("render_error", `k${i}`);
    }

    expect(send).toHaveBeenCalledTimes(20);
  });
});
