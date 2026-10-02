// @vitest-environment jsdom

import axe from "axe-core";
import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, type, unmount } from "../management/testKit";
import { createPurchaseInvoiceClient, type PurchaseInvoiceClient } from "./api";
import { PurchaseInvoicesSection } from "./index";
import type { PurchaseInvoice, PurchaseInvoiceLine, PurchaseInvoiceSummary } from "./models";

const line = (over: Partial<PurchaseInvoiceLine> = {}): PurchaseInvoiceLine => ({
  lineId: "l1", lineNumber: 1, description: "Süt koli", quantity: 5, unitCode: "BX", unitPrice: 120, lineNet: 600,
  stockItemId: null, conversionFactor: null, ...over,
});
const invoice = (over: Partial<PurchaseInvoice> = {}): PurchaseInvoice => ({
  invoiceId: "i1", invoiceNumber: "ABC001", issueDate: "2026-09-20", supplierName: "Anadolu Gıda", supplierId: "s1", status: "Draft",
  currency: "TRY", lines: [line()], ...over,
});
const summary = (over: Partial<PurchaseInvoiceSummary> = {}): PurchaseInvoiceSummary => ({
  invoiceId: "i1", invoiceNumber: "ABC001", issueDate: "2026-09-20", supplierName: "Anadolu Gıda", supplierId: "s1", status: "Draft",
  lineCount: 1, unmappedLineCount: 1, netTotal: 600, ...over,
});

const client = (over: Partial<PurchaseInvoiceClient> = {}): PurchaseInvoiceClient => ({
  importXml: vi.fn().mockResolvedValue(invoice()),
  list: vi.fn().mockResolvedValue([summary()]),
  get: vi.fn().mockResolvedValue(invoice()),
  mapLine: vi.fn().mockResolvedValue(invoice({ lines: [line({ stockItemId: "m1", conversionFactor: 24 })] })),
  approve: vi.fn().mockResolvedValue(undefined),
  reject: vi.fn().mockResolvedValue(undefined),
  listLocations: vi.fn().mockResolvedValue([{ id: "d1", name: "Ana depo" }]),
  listStockItems: vi.fn().mockResolvedValue([{ id: "m1", name: "Süt", trackingUnitCode: "l" }]),
  ...over,
});

const openFirst = async () => { await press("Aç"); };

describe("purchase invoices section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the tab only for purchasing.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["purchasing.manage"])} />);
    expect(buttons()).toContain("Alış faturaları");
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).not.toContain("Alış faturaları");
  });

  it("lists drafts in Turkish and shows the unmapped count, with no accessibility violations", async () => {
    await render(<PurchaseInvoicesSection client={client()} />);
    const row = document.querySelector("tbody tr")!.textContent!;
    expect(row).toContain("ABC001");
    expect(row).toContain("Taslak");
    expect(row).not.toContain("Draft");
    await openFirst();
    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("uploading an XML file sends its text and opens the new draft", async () => {
    const api = client();
    await render(<PurchaseInvoicesSection client={api} />);
    const input = document.getElementById("mpi-file") as HTMLInputElement;
    const file = new File(["<Invoice/>"], "fatura.xml", { type: "text/xml" });
    Object.defineProperty(file, "text", { value: () => Promise.resolve("<Invoice/>") });
    Object.defineProperty(input, "files", { value: [file], configurable: true });
    await act(async () => { input.dispatchEvent(new Event("change", { bubbles: true })); });
    expect(api.importXml).toHaveBeenCalledWith("<Invoice/>");
    expect(document.body.textContent).toContain("Fatura taslak olarak alındı.");
    expect(document.querySelector('section[aria-label="Fatura"]')).not.toBeNull();
  });

  it("approval stays disabled until every line is mapped and a depot is chosen, then approves", async () => {
    const api = client();
    await render(<PurchaseInvoicesSection client={api} />);
    await openFirst();
    const approve = () => [...document.querySelectorAll("button")].find((b) => b.textContent === "Onayla ve stoğa gir")!;
    expect(approve().disabled).toBe(true);
    await type("mpi-location", "d1");
    expect(approve().disabled).toBe(true);
    expect(document.body.textContent).toContain("1 satır henüz eşleşmedi");

    await type("mpi-stock-l1", "m1");
    await type("mpi-factor-l1", "24");
    await press("Eşleştir");
    expect(api.mapLine).toHaveBeenCalledWith("i1", "l1", "m1", 24);
    expect(approve().disabled).toBe(false);
    await type("mpi-location", "");
    expect(approve().disabled).toBe(true);
    await type("mpi-location", "d1");
    await press("Onayla ve stoğa gir");
    expect(api.approve).toHaveBeenCalledWith("i1", "d1");
    expect(document.body.textContent).toContain("Fatura onaylandı ve stoğa girdi.");
  });

  it("refuses a missing item or a non-positive factor without calling the server", async () => {
    const api = client();
    await render(<PurchaseInvoicesSection client={api} />);
    await openFirst();
    await press("Eşleştir");
    expect(alertText()).toBe("Önce bir hammadde seçin.");
    await type("mpi-stock-l1", "m1");
    await type("mpi-factor-l1", "0");
    await press("Eşleştir");
    expect(alertText()).toBe("Çevrim katsayısı sıfırdan büyük bir sayı olmalı.");
    expect(api.mapLine).not.toHaveBeenCalled();
  });

  it("warns that the supplier must be registered first", async () => {
    await render(<PurchaseInvoicesSection client={client({ get: vi.fn().mockResolvedValue(invoice({ supplierId: null })) })} />);
    await openFirst();
    expect(document.body.textContent).toContain("kayıtlı bir tedarikçi yok");
  });

  it("rejects a draft", async () => {
    const api = client();
    await render(<PurchaseInvoicesSection client={api} />);
    await openFirst();
    await press("Reddet");
    expect(api.reject).toHaveBeenCalledWith("i1");
    expect(document.body.textContent).toContain("Fatura reddedildi.");
  });

  it("an approved invoice offers no mapping, approval or rejection", async () => {
    const approved = invoice({ status: "Approved", lines: [line({ stockItemId: "m1", conversionFactor: 24 })] });
    await render(<PurchaseInvoicesSection client={client({ get: vi.fn().mockResolvedValue(approved), list: vi.fn().mockResolvedValue([summary({ status: "Approved" })]) })} />);
    await openFirst();
    expect(buttons()).not.toContain("Onayla ve stoğa gir");
    expect(buttons()).not.toContain("Reddet");
    expect(document.body.textContent).toContain("Onaylandı");
    expect(document.body.textContent).toContain("Süt");
  });

  it("shows the server's Turkish reason when an action fails", async () => {
    const failing = client({ approve: vi.fn().mockRejectedValue(new ManagementApiError(409, "INVOICE_NOT_READY", "Fatura onaya hazır değil.")) });
    await render(<PurchaseInvoicesSection client={failing} />);
    await openFirst();
    await type("mpi-stock-l1", "m1");
    await press("Eşleştir");
    await type("mpi-location", "d1");
    await press("Onayla ve stoğa gir");
    expect(alertText()).toBe("Fatura onaya hazır değil.");
  });

  it("the client asks the purchase invoice addresses and sends an idempotency key on writes", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response("{}", { status: 200 })));
    const api = createPurchaseInvoiceClient(fetcher as unknown as typeof fetch);
    await api.list("Draft");
    await api.approve("i1", "d1");
    const calls = fetcher.mock.calls as unknown as [string, RequestInit][];
    expect(calls[0][0]).toBe("/api/v1/management/purchasing/purchase-invoices?status=Draft");
    expect(calls[1][0]).toBe("/api/v1/management/purchasing/purchase-invoices/i1/approve");
    expect(JSON.parse(calls[1][1].body as string)).toMatchObject({ locationId: "d1", idempotencyKey: expect.any(String) });
  });
});
