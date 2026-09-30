// @vitest-environment jsdom

import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, type, unmount } from "../management/testKit";
import { createOrderInvoicesClient, type OrderInvoicesClient } from "./api";
import { OrderInvoicesSection } from "./index";
import type { OrderInvoiceDetail, OrderInvoiceList } from "./models";

const list = (over: Partial<OrderInvoiceList> = {}): OrderInvoiceList => ({
  from: "2026-09-23", to: "2026-09-29",
  invoices: [
    { invoiceId: "i1", orderId: "o1", orderNumber: "YS-1", provider: "yemeksepeti", issueDate: "2026-09-29", serviceDate: "2026-09-28", status: "Draft", netAmount: 100, taxAmount: 10, grossAmount: 110 },
    { invoiceId: "i2", orderId: "o2", orderNumber: "TG-2", provider: "trendyol-go", issueDate: "2026-09-29", serviceDate: "2026-09-29", status: "Draft", netAmount: 50, taxAmount: 5, grossAmount: 55 },
  ],
  missing: [{ orderId: "o3", orderNumber: "YS-3", provider: "yemeksepeti", total: 80, deliveredAt: "2026-09-27T12:00:00+03:00", daysLeft: 5 }],
  ...over,
});

const detail: OrderInvoiceDetail = {
  orderNumber: "YS-1", provider: "yemeksepeti", webAddress: "https://www.yemeksepeti.com", serviceDate: "2026-09-28",
  seller: { legalName: "Deniz Lokantası Ltd. Şti.", taxIdNumber: "1234567890", taxOffice: "Kadıköy", address: "Moda Cad. 1" },
  lines: [{ lineNumber: 1, description: "Lahmacun", quantity: 2, taxRate: 10, grossAmount: 110 }],
};

const client = (over: Partial<OrderInvoicesClient> = {}): OrderInvoicesClient => ({
  list: vi.fn().mockResolvedValue(list()), detail: vi.fn().mockResolvedValue(detail), ...over,
});

describe("online order invoices section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the tab for a session holding reports.view only", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).toContain("Online faturalar");
    await render(<ManagementArea capabilities={new Set(["orders.create"])} />);
    expect(buttons()).not.toContain("Online faturalar");
  });

  it("asks for nothing until requested, then lists the drafts and the waiting orders in Turkish without raw values", async () => {
    const api = client();
    await render(<OrderInvoicesSection client={api} />);
    expect(api.list).not.toHaveBeenCalled();
    await press("Listele");
    const rows = [...document.querySelectorAll("section[aria-label='Online sipariş fatura taslakları'] tbody tr")].map((row) => row.textContent);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Yemeksepeti");
    expect(rows[0]).toContain("Taslak");
    expect(rows[1]).toContain("Trendyol Go");
    const waiting = document.querySelector("section[aria-label='Faturası henüz açılmamış siparişler']")!.textContent!;
    expect(waiting).toContain("YS-3");
    expect(waiting).toContain("5");
    expect(waiting).toContain("elle düzenlemeniz gerekir");
    expect(document.body.textContent).toContain("gönderim henüz yapılmaz");
    for (const raw of ["yemeksepeti", "trendyol-go", "Draft"]) expect(document.body.textContent).not.toContain(raw);
    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("shows no waiting section when every delivered order has a draft", async () => {
    await render(<OrderInvoicesSection client={client({ list: vi.fn().mockResolvedValue(list({ missing: [] })) })} />);
    await press("Listele");
    expect(document.body.textContent).not.toContain("Faturası henüz açılmamış siparişler");
  });

  it("opens one draft's seller, address and lines", async () => {
    const api = client();
    await render(<OrderInvoicesSection client={api} />);
    await press("Listele");
    await press("Göster");
    expect(api.detail).toHaveBeenCalledWith("o1");
    const text = document.querySelector("section[aria-label='Fatura ayrıntısı']")!.textContent!;
    expect(text).toContain("Deniz Lokantası Ltd. Şti.");
    expect(text).toContain("Moda Cad. 1");
    expect(text).toContain("https://www.yemeksepeti.com");
    expect(text).toContain("Lahmacun");
  });

  it("sends the chosen dates", async () => {
    const api = client();
    await render(<OrderInvoicesSection client={api} />);
    await type("moi-from", "2026-09-01");
    await type("moi-to", "2026-09-10");
    await press("Listele");
    expect(api.list).toHaveBeenCalledWith("2026-09-01", "2026-09-10");
  });

  it("refuses a range over 31 days or an inverted one in Turkish without asking the server", async () => {
    const api = client();
    await render(<OrderInvoicesSection client={api} />);
    await type("moi-from", "2026-08-01");
    await type("moi-to", "2026-09-10");
    await press("Listele");
    expect(alertText()).toBe("Liste en çok 31 günü kapsayabilir.");
    await type("moi-from", "2026-09-10");
    await type("moi-to", "2026-09-01");
    await press("Listele");
    expect(alertText()).toContain("Başlangıç tarihi bitişten sonra olamaz");
    expect(api.list).not.toHaveBeenCalled();
  });

  it("says so when the range has no draft", async () => {
    await render(<OrderInvoicesSection client={client({ list: vi.fn().mockResolvedValue(list({ invoices: [], missing: [] })) })} />);
    await press("Listele");
    expect(document.body.textContent).toContain("Bu aralıkta fatura taslağı yok.");
    expect(document.querySelector("table")).toBeNull();
  });

  it("shows the server's Turkish reason when the list or a detail cannot be read", async () => {
    const failing = client({ list: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Rapor görüntüleme izni gerekiyor.")) });
    await render(<OrderInvoicesSection client={failing} />);
    await press("Listele");
    expect(alertText()).toBe("Rapor görüntüleme izni gerekiyor.");

    await unmount();
    const noDetail = client({ detail: vi.fn().mockRejectedValue(new ManagementApiError(404, "NOT_FOUND", "Bu sipariş için fatura taslağı yok.")) });
    await render(<OrderInvoicesSection client={noDetail} />);
    await press("Listele");
    await press("Göster");
    expect(alertText()).toBe("Bu sipariş için fatura taslağı yok.");
  });

  it("the client asks the order invoice addresses", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("{}", { status: 200 })));
    const api = createOrderInvoicesClient(fetcher as unknown as typeof fetch);
    await api.list("2026-09-01", "2026-09-07");
    await api.detail("o1");
    expect((fetcher.mock.calls[0] as unknown[])[0]).toBe("/api/v1/management/order-invoices?from=2026-09-01&to=2026-09-07");
    expect((fetcher.mock.calls[1] as unknown[])[0]).toBe("/api/v1/management/order-invoices/by-order/o1");
  });
});
