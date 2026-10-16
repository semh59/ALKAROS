// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { installInteractionTelemetry } from "./installInteractionTelemetry";
import { buildNameTable } from "./interactionKeys";
import { createInteractionRecorder } from "./interactionRecorder";

interface Sent { key: string; kind: string; outcome: string; durationMs: number; traceId?: string }

function setup(html: string, traceListeners: ((id: string) => void)[] = []) {
  document.body.innerHTML = html;
  window.history.replaceState(null, "", "/cashier/orders/42");
  const bodies: string[] = [];
  const recorder = createInteractionRecorder({
    send: (body) => { bodies.push(body); return true; },
    storage: { getItem: () => null, setItem: () => undefined },
  });
  const uninstall = installInteractionTelemetry({
    recorder,
    names: buildNameTable({ common: { save: "Kaydet" } }),
    afterPaint: (callback) => callback(),
    traceSource: (listener) => { traceListeners.push(listener); return () => undefined; },
  });
  const items = () => {
    recorder.flush();
    return bodies.flatMap((body) => (JSON.parse(body) as { items: Sent[] }).items);
  };
  return { uninstall, items, bodies };
}

describe("install interaction telemetry", () => {
  it("attaches the trace id of the first API call a click started and no later one", () => {
    const issue: ((id: string) => void)[] = [];
    const s = setup('<button id="a" data-telemetry-id="pay.confirm">Öde</button><button id="b" data-telemetry-id="other">Diğer</button>', issue);
    uninstall = s.uninstall;
    const first = "1".repeat(32);

    document.getElementById("a")!.click();
    issue.forEach((listener) => { listener(first); listener("2".repeat(32)); });
    document.getElementById("b")!.click();

    const items = s.items();
    expect(items.find((i) => i.key === "pay.confirm")!.traceId).toBe(first);
    expect(items.find((i) => i.key === "other")!.traceId).toBeUndefined();
  });

  it("keeps the click without an id when no call follows, when the call is too late, and for route changes", () => {
    const issue: ((id: string) => void)[] = [];
    const s = setup('<button id="a" data-telemetry-id="late">Geç</button>', issue);
    uninstall = s.uninstall;
    const now = vi.spyOn(Date, "now");

    now.mockReturnValue(1_000);
    document.getElementById("a")!.click();
    now.mockReturnValue(3_500);
    issue.forEach((listener) => listener("3".repeat(32)));
    now.mockReturnValue(3_600);
    window.history.pushState(null, "", "/cashier/tables/7");
    issue.forEach((listener) => listener("4".repeat(32)));
    now.mockRestore();

    const items = s.items();
    expect(items.map((i) => i.key)).toEqual(["late", "nav@/cashier/tables/:n"]);
    expect(items.every((i) => i.traceId === undefined)).toBe(true);
  });

  it("stores the trace id that arrives after the click was already recorded", () => {
    const issue: ((id: string) => void)[] = [];
    const saved: string[] = [];
    document.body.innerHTML = '<button id="a" data-telemetry-id="pay.confirm">Öde</button>';
    const bodies: string[] = [];
    const recorder = createInteractionRecorder({
      send: (body) => { bodies.push(body); return true; },
      storage: { getItem: () => null, setItem: (_key, value) => { saved.push(value); } },
    });
    uninstall = installInteractionTelemetry({
      recorder, names: buildNameTable({}), afterPaint: (callback) => callback(),
      traceSource: (listener) => { issue.push(listener); return () => undefined; },
    });

    document.getElementById("a")!.click();
    issue.forEach((listener) => listener("5".repeat(32)));

    expect(saved.at(-1)).toContain("5".repeat(32));
    recorder.flush();
    expect(bodies.join("")).toContain("5".repeat(32));
  });

  let uninstall: (() => void) | undefined;
  afterEach(() => { uninstall?.(); uninstall = undefined; });

  it("records a click by key and never sends the visible text", () => {
    const s = setup('<button id="a">Kaydet</button><button id="b">Ali Veli hesabı</button>');
    uninstall = s.uninstall;

    document.getElementById("a")!.click();
    document.getElementById("b")!.click();

    const items = s.items();
    expect(items.map((i) => i.key)).toEqual(["t:common.save", "auto:button@/cashier/orders/:n"]);
    expect(items.every((i) => i.kind === "click" && i.outcome === "ok" && i.durationMs >= 0)).toBe(true);
    expect(s.bodies.join("")).not.toMatch(/Ali|Veli|Kaydet/);
  });

  it("ignores clicks on things that are not controls", () => {
    const s = setup("<p id='p'>metin</p><div id='d'><span id='s'>x</span></div>");
    uninstall = s.uninstall;

    document.getElementById("s")!.click();
    document.getElementById("p")!.click();

    expect(s.items()).toEqual([]);
  });

  it("uses data-telemetry-id and records submits and route changes", () => {
    const s = setup('<form id="f"><button type="button" data-telemetry-id="pay.confirm">Öde</button></form>');
    uninstall = s.uninstall;

    document.querySelector<HTMLButtonElement>("button")!.click();
    document.getElementById("f")!.dispatchEvent(new Event("submit", { bubbles: true }));
    window.history.pushState(null, "", "/cashier/tables/7");

    expect(s.items().map((i) => `${i.kind}:${i.key}`)).toEqual([
      "click:pay.confirm",
      "submit:auto:form@/cashier/orders/:n",
      "navigate:nav@/cashier/tables/:n",
    ]);
  });

  it("marks an interaction that overlaps an uncaught error as error", () => {
    const pending: (() => void)[] = [];
    document.body.innerHTML = '<button id="a">Kaydet</button>';
    const bodies: string[] = [];
    const recorder = createInteractionRecorder({ send: (b) => { bodies.push(b); return true; }, storage: { getItem: () => null, setItem: () => undefined } });
    uninstall = installInteractionTelemetry({ recorder, names: buildNameTable({}), afterPaint: (callback) => pending.push(callback) });

    document.getElementById("a")!.click();
    window.dispatchEvent(new Event("error"));
    pending.forEach((callback) => callback());
    recorder.flush();

    expect((JSON.parse(bodies[0]!) as { items: Sent[] }).items[0]!.outcome).toBe("error");
  });

  it("stops recording after uninstall", () => {
    const s = setup('<button id="a">Kaydet</button>');
    s.uninstall();

    document.getElementById("a")!.click();

    expect(s.items()).toEqual([]);
  });
});
