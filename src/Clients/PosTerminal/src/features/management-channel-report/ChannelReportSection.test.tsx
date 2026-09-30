// @vitest-environment jsdom

import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, type, unmount } from "../management/testKit";
import { createChannelReportClient, type ChannelReportClient } from "./api";
import { ChannelReportSection } from "./index";
import type { ChannelDayRow, ChannelReport } from "./models";

const day = (over: Partial<ChannelDayRow>): ChannelDayRow => ({
  businessDate: "2026-09-29", source: "Online", provider: "yemeksepeti",
  ordersReceived: 5, awaitingConfirmation: 0, accepted: 3, rejected: 1, cancelled: 1,
  acceptedValue: 600, acceptedNetValue: 500, cancelledValue: 90, providerRefused: 2, ...over,
});

const report = (over: Partial<ChannelReport> = {}): ChannelReport => ({
  from: "2026-09-23", to: "2026-09-29", check: { isBalanced: true },
  days: [day({}), day({ source: "Online", provider: "trendyol-go", ordersReceived: 2, accepted: 2, rejected: 0, cancelled: 0, acceptedValue: 300, acceptedNetValue: 250, providerRefused: 0 }),
    day({ source: "Qr", provider: null, ordersReceived: 4, accepted: 4, rejected: 0, cancelled: 0, acceptedValue: 200, acceptedNetValue: 180, providerRefused: 0 })],
  ...over,
});

const client = (over: Partial<ChannelReportClient> = {}): ChannelReportClient => ({ report: vi.fn().mockResolvedValue(report()), ...over });

describe("channel report section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the channel report tab for a session holding reports.view", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).toContain("Kanal raporu");
    await render(<ManagementArea capabilities={new Set(["orders.create"])} />);
    expect(buttons()).not.toContain("Kanal raporu");
  });

  it("asks for nothing until the report is requested, then lists every row and the totals in Turkish", async () => {
    const api = client();
    await render(<ChannelReportSection client={api} />);
    expect(api.report).not.toHaveBeenCalled();
    await press("Raporu getir");
    const rows = [...document.querySelectorAll("tbody tr")].map((row) => row.textContent);
    expect(rows).toHaveLength(3);
    expect(rows[0]).toContain("Yemeksepeti");
    expect(rows[1]).toContain("Trendyol Go");
    expect(rows[2]).toContain("QR");
    const total = document.querySelector("tfoot tr")!.textContent!;
    expect(total).toContain("Toplam");
    expect(total).toContain("11");
    expect(total).toContain("9");
    expect(document.body.textContent).toContain("ciroya ve kasaya dahil değildir");
    for (const raw of ["yemeksepeti", "trendyol-go", "Accepted", "Qr"]) expect(document.body.textContent).not.toContain(raw);
    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("sends the chosen dates and channel", async () => {
    const api = client();
    await render(<ChannelReportSection client={api} />);
    await type("mcr-from", "2026-09-01");
    await type("mcr-to", "2026-09-10");
    await type("mcr-source", "Online");
    await press("Raporu getir");
    expect(api.report).toHaveBeenCalledWith("2026-09-01", "2026-09-10", "Online");
  });

  it("refuses a range over 31 days or an inverted one in Turkish without asking the server", async () => {
    const api = client();
    await render(<ChannelReportSection client={api} />);
    await type("mcr-from", "2026-08-01");
    await type("mcr-to", "2026-09-10");
    await press("Raporu getir");
    expect(alertText()).toBe("Rapor en çok 31 günü kapsayabilir.");
    await type("mcr-from", "2026-09-10");
    await type("mcr-to", "2026-09-01");
    await press("Raporu getir");
    expect(alertText()).toContain("Başlangıç tarihi bitişten sonra olamaz");
    expect(api.report).not.toHaveBeenCalled();
  });

  it("warns when the report totals do not match the order records", async () => {
    await render(<ChannelReportSection client={client({ report: vi.fn().mockResolvedValue(report({ check: { isBalanced: false } })) })} />);
    await press("Raporu getir");
    expect(alertText()).toContain("Rapor toplamları sipariş kayıtlarıyla tutmuyor");
  });

  it("says so when the range has no rows", async () => {
    await render(<ChannelReportSection client={client({ report: vi.fn().mockResolvedValue(report({ days: [] })) })} />);
    await press("Raporu getir");
    expect(document.body.textContent).toContain("Bu aralıkta kayıt yok.");
    expect(document.querySelector("table")).toBeNull();
  });

  it("shows the server's Turkish reason when the report cannot be read", async () => {
    const failing = client({ report: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Rapor görüntüleme izni gerekiyor.")) });
    await render(<ChannelReportSection client={failing} />);
    await press("Raporu getir");
    expect(alertText()).toBe("Rapor görüntüleme izni gerekiyor.");
  });

  it("the client asks the channel report address with the range and channel", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("{}", { status: 200 })));
    await createChannelReportClient(fetcher as unknown as typeof fetch).report("2026-09-01", "2026-09-07", "Qr");
    expect((fetcher.mock.calls[0] as unknown[])[0]).toBe("/api/v1/management/reports/channels?from=2026-09-01&to=2026-09-07&source=Qr");
  });
});
