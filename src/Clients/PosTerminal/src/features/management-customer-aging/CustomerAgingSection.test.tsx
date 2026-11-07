// @vitest-environment jsdom

import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, unmount } from "../management/testKit";
import type { CustomerAgingClient } from "./api";
import { CustomerAgingSection } from "./index";
import type { CustomerAgingReport, CustomerAgingRow } from "./models";

const row = (over: Partial<CustomerAgingRow>): CustomerAgingRow => ({
  customerId: "c1", name: "Borçlu Müşteri",
  days0To30: 20, days31To60: 50, days61To90: 0, over90: 70, totalDebt: 140, unappliedCredit: 0,
  balance30DaysAgo: 150, balance60DaysAgo: null, balance90DaysAgo: null, ...over,
});

const report = (over: Partial<CustomerAgingReport> = {}): CustomerAgingReport => ({
  asOf: "2026-11-07", check: { isBalanced: true },
  rows: [row({}), row({ customerId: "c2", name: "Peşin Ödeyen", days0To30: 0, days31To60: 0, over90: 0, totalDebt: 0, unappliedCredit: 25, balance30DaysAgo: null })],
  ...over,
});

const client = (over: Partial<CustomerAgingClient> = {}): CustomerAgingClient => ({ report: vi.fn().mockResolvedValue(report()), ...over });

describe("customer aging section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the tab only for a session holding reports.view", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).toContain("Cari yaşlandırma");
    await render(<ManagementArea capabilities={new Set(["orders.create"])} />);
    expect(buttons()).not.toContain("Cari yaşlandırma");
  });

  it("lists every customer with buckets, credit, snapshot balances and totals in Turkish", async () => {
    await render(<CustomerAgingSection client={client()} />);
    const rows = [...document.querySelectorAll("tbody tr")].map((r) => r.textContent!);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Borçlu Müşteri");
    expect(rows[0]).toContain("140,00");
    expect(rows[0]).toContain("150,00");
    expect(rows[0]).toContain("—");
    expect(rows[1]).toContain("25,00");
    const total = document.querySelector("tfoot tr")!.textContent!;
    expect(total).toContain("Toplam");
    expect([...document.querySelectorAll("tfoot td")].map((cell) => cell.textContent)).toEqual(
      ["Toplam", "₺20,00", "₺50,00", "₺0,00", "₺70,00", "₺140,00", "₺25,00", "—"]);
    expect([...document.querySelectorAll("thead th")].map((cell) => cell.textContent)).toContain("90 gün önceki bakiye");
    expect(document.body.textContent).toContain("Rapor tarihi: 07.11.2026");
    expect(document.body.textContent).toContain("vade tarihine göre değil");
    expect(document.querySelector('[role="alert"]')).toBeNull();
    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("warns when the report does not match the balance records", async () => {
    await render(<CustomerAgingSection client={client({ report: vi.fn().mockResolvedValue(report({ check: { isBalanced: false } })) })} />);
    expect(alertText()).toContain("rakamlara güvenmeyin");
  });

  it("says so when nobody has a balance", async () => {
    await render(<CustomerAgingSection client={client({ report: vi.fn().mockResolvedValue(report({ rows: [] })) })} />);
    expect(document.body.textContent).toContain("Bakiyesi olan cari müşteri yok.");
    expect(document.querySelector("table")).toBeNull();
  });

  it("shows the server's Turkish reason on failure and loads again on refresh", async () => {
    const failing = vi.fn().mockRejectedValueOnce(new ManagementApiError(403, "FORBIDDEN", "Cari yaşlandırma raporu için rapor görüntüleme izni gerekiyor.")).mockResolvedValue(report());
    await render(<CustomerAgingSection client={client({ report: failing })} />);
    expect(document.body.textContent).toContain("Cari yaşlandırma raporu için rapor görüntüleme izni gerekiyor.");
    await press("Yenile");
    expect(failing).toHaveBeenCalledTimes(2);
    expect(document.querySelectorAll("tbody tr")).toHaveLength(2);
  });
});
