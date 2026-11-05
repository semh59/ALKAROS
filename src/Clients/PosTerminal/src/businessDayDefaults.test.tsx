// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ClosingSection } from "./features/management-closing";
import { MenusSection, RecipeCostSection } from "./features/management-menus-recipe-cost";
import { StockSection } from "./features/management-stock";
import { render, unmount } from "./features/management/testKit";

// Requests never settle: only the initial field values are under test.
const lenient = <T extends object>(settled: Record<string, unknown> = {}) =>
  new Proxy({} as T, { get: (_, name: string) => name in settled ? vi.fn().mockResolvedValue(settled[name]) : vi.fn(() => new Promise(() => {})) });
const value = (id: string) => (document.getElementById(id) as HTMLInputElement).value;

describe("default business dates use the Istanbul business day", () => {
  beforeEach(() => { vi.useFakeTimers({ toFake: ["Date"] }); process.env.TZ = "America/Los_Angeles"; });
  afterEach(async () => { await unmount(); vi.useRealTimers(); });

  // 02:00 UTC is 05:00 in Istanbul: still the previous business day; 03:00 UTC is the 06:00 cutoff.
  it.each([
    ["2026-09-24T02:00:00Z", "2026-09-23"],
    ["2026-09-24T03:00:00Z", "2026-09-24"],
  ])("closing, stock, menus and recipe cost at %s default to %s", async (instant, day) => {
    vi.setSystemTime(new Date(instant));
    await render(<ClosingSection capabilities={new Set(["reports.view"])} client={lenient()} />);
    expect(value("closing-date")).toBe(day);
    await render(<StockSection capabilities={new Set(["inventory.manage"])} client={lenient({ getVariance: { items: [], excludedForMissingCountsCount: 0 } })} />);
    expect([value("stock-from"), value("stock-to")]).toEqual([day, day]);
    await render(<MenusSection capabilities={new Set(["menu.manage"])} client={lenient({ getDailyMenu: null })} />);
    expect(value("d-date")).toBe(day);
    await render(<RecipeCostSection capabilities={new Set(["inventory.manage"])} client={lenient({ listRecipeVersions: [{ id: "v1", label: "Tarif", status: "Active" }] })} />);
    expect(value("c-date")).toBe(day);
  });
});
