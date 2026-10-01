// @vitest-environment jsdom

import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, type, unmount } from "../management/testKit";
import { createProductMarginClient, type ProductMarginClient } from "./api";
import { ProductMarginSection } from "./index";
import type { ProductMarginReport, ProductMarginRow } from "./models";

const row = (over: Partial<ProductMarginRow>): ProductMarginRow => ({
  productId: "p1", productName: "Burger", soldQuantity: 3, givenAwayQuantity: 1, netRevenue: 450,
  cost: 120, unknownCostLines: 0, grossMargin: 330, marginPercent: 73.33, ...over,
});

const report = (over: Partial<ProductMarginReport> = {}): ProductMarginReport => ({
  reportVersion: "product-margin.v1", from: "2026-09-23", to: "2026-09-29",
  rows: [row({}), row({ productId: "p2", productName: "Ayran", netRevenue: 60, cost: 0, unknownCostLines: 2, grossMargin: null, marginPercent: null })],
  totalNetRevenue: 510, totalCost: 120, unknownCostLines: 2,
  check: { productNetTotal: 510, linesNetTotal: 510, billLevelDiscounts: 25, isBalanced: true },
  ...over,
});

const client = (over: Partial<ProductMarginClient> = {}): ProductMarginClient => ({ report: vi.fn().mockResolvedValue(report()), ...over });

describe("product margin section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the tab for a session holding reports.view only", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).toContain("Ürün kârlılığı");
    await render(<ManagementArea capabilities={new Set(["orders.create"])} />);
    expect(buttons()).not.toContain("Ürün kârlılığı");
  });

  it("asks for nothing until requested, then shows rows, totals and the unknown-cost warning in Turkish", async () => {
    const api = client();
    await render(<ProductMarginSection client={api} />);
    expect(api.report).not.toHaveBeenCalled();
    await press("Raporu getir");
    const rows = [...document.querySelectorAll("tbody tr")].map((r) => r.textContent!);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Burger");
    expect(rows[0]).toContain("%73,33");
    const unknownCells = [...document.querySelectorAll("tbody tr")[1].querySelectorAll("td")].map((c) => c.textContent);
    expect(unknownCells.slice(5)).toEqual(["Bilinmiyor", "Bilinmiyor"]);
    expect(unknownCells[4]).toContain("eksik");
    expect(alertText()).toContain("Maliyeti bilinmeyen satır sayısı");
    expect(document.querySelector("tfoot")!.textContent).toContain("Toplam");
    expect(document.body.textContent).toContain("Fiş düzeyi indirimler ürünlere dağıtılmaz");
    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("sends the chosen dates", async () => {
    const api = client();
    await render(<ProductMarginSection client={api} />);
    await type("mpm-from", "2026-09-01");
    await type("mpm-to", "2026-09-10");
    await press("Raporu getir");
    expect(api.report).toHaveBeenCalledWith("2026-09-01", "2026-09-10");
  });

  it("refuses a range over 31 days or an inverted one without asking the server", async () => {
    const api = client();
    await render(<ProductMarginSection client={api} />);
    await type("mpm-from", "2026-08-01");
    await type("mpm-to", "2026-09-10");
    await press("Raporu getir");
    expect(alertText()).toBe("Rapor en çok 31 günü kapsayabilir.");
    await type("mpm-from", "2026-09-10");
    await type("mpm-to", "2026-09-01");
    await press("Raporu getir");
    expect(alertText()).toContain("Başlangıç tarihi bitişten sonra olamaz");
    expect(api.report).not.toHaveBeenCalled();
  });

  it("warns when the report totals do not match", async () => {
    await render(<ProductMarginSection client={client({ report: vi.fn().mockResolvedValue(report({ check: { productNetTotal: 1, linesNetTotal: 2, billLevelDiscounts: 0, isBalanced: false } })) })} />);
    await press("Raporu getir");
    expect(document.body.textContent).toContain("Rapor toplamları fiş kayıtlarıyla tutmuyor");
  });

  it("says so when the range has no rows", async () => {
    await render(<ProductMarginSection client={client({ report: vi.fn().mockResolvedValue(report({ rows: [], unknownCostLines: 0 })) })} />);
    await press("Raporu getir");
    expect(document.body.textContent).toContain("Bu aralıkta kayıt yok.");
    expect(document.querySelector("table")).toBeNull();
  });

  it("shows the server's Turkish reason when the report cannot be read", async () => {
    const failing = client({ report: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Rapor görüntüleme izni gerekiyor.")) });
    await render(<ProductMarginSection client={failing} />);
    await press("Raporu getir");
    expect(alertText()).toBe("Rapor görüntüleme izni gerekiyor.");
  });

  it("the client asks the product margin address with the range", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("{}", { status: 200 })));
    await createProductMarginClient(fetcher as unknown as typeof fetch).report("2026-09-01", "2026-09-07");
    expect((fetcher.mock.calls[0] as unknown[])[0]).toBe("/api/v1/management/reports/product-margin?from=2026-09-01&to=2026-09-07");
  });
});
