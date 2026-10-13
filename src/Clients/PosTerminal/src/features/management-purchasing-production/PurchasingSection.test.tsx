// @vitest-environment jsdom

import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { createPurchasingClient, type PurchasingClient } from "./api";
import { PurchasingSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const order = (status: string) => ({
  id: "o1", orderNumber: "PO-1", supplierId: "s1", status, destinationLocationId: "l1", totalAmount: 250, currency: "TRY",
  lines: [{ id: "ln1", stockItemId: "i1", orderedQuantity: 10, receivedQuantity: 4, openQuantity: 6, unitCode: "kg", unitPrice: 25, totalPrice: 250, status: "PartiallyReceived" }],
});

function fakeClient(status = "Submitted", overrides: Partial<PurchasingClient> = {}): PurchasingClient {
  return {
    listSuppliers: vi.fn().mockResolvedValue([{ id: "s1", code: "TED-1", name: "Un Fabrikası", active: true }]),
    createSupplier: vi.fn().mockResolvedValue(undefined),
    setSupplierActive: vi.fn().mockResolvedValue(undefined),
    getSupplier: vi.fn().mockResolvedValue({ id: "s1", code: "TED-1", name: "Un Fabrikası", taxNumber: "1234567890", taxOffice: "Kadıköy", phone: "02120000000", email: "ted@example.com", active: true, isMasked: false }),
    updateSupplier: vi.fn().mockResolvedValue(undefined),
    listOrders: vi.fn().mockResolvedValue([order(status)]),
    createOrder: vi.fn().mockResolvedValue(undefined),
    submitOrder: vi.fn().mockResolvedValue(undefined),
    cancelOrder: vi.fn().mockResolvedValue(undefined),
    receiveGoods: vi.fn().mockResolvedValue(undefined),
    listLocations: vi.fn().mockResolvedValue([{ id: "l1", name: "Ana depo" }]),
    listStockItems: vi.fn().mockResolvedValue([{ id: "i1", name: "Un", trackingUnitCode: "kg" }]),
    ...overrides,
  };
}

const none = new Set<string>();

describe("purchasing section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the purchasing tab only for a session holding purchasing.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).not.toContain("Satın alma");
    await render(<ManagementArea capabilities={new Set(["reports.view", "purchasing.manage"])} />);
    expect(buttons()).toContain("Satın alma");
  });

  it("shows Turkish statuses and no raw enum value", async () => {
    await render(<PurchasingSection capabilities={none} client={fakeClient("PartiallyReceived")} />);
    const text = document.body.textContent ?? "";
    expect(text).toContain("Kısmen teslim alındı");
    expect(text).not.toContain("PartiallyReceived");
  });

  it("offers submit only for a draft and receiving only for a submitted order", async () => {
    await render(<PurchasingSection capabilities={none} client={fakeClient("Draft")} />);
    expect(buttons()).toContain("Gönder");
    expect(buttons()).not.toContain("Mal kabul");
    await render(<PurchasingSection capabilities={none} client={fakeClient("Submitted")} />);
    expect(buttons()).toContain("Mal kabul");
    expect(buttons()).not.toContain("Gönder");
    await render(<PurchasingSection capabilities={none} client={fakeClient("Completed")} />);
    for (const forbidden of ["Gönder", "Mal kabul", "İptal et"]) expect(buttons()).not.toContain(forbidden);
  });

  it("a goods receipt sends the open quantity by default with the manager approval flag", async () => {
    const client = fakeClient();
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Mal kabul");
    await type("r-number", "MK-1");
    await type("v-ln1", "Ambalaj hasarlı");
    await act(async () => (document.querySelector('input[type="checkbox"]') as HTMLInputElement).click());
    await submit("Mal kabul PO-1");
    expect(client.receiveGoods).toHaveBeenCalledWith("o1", {
      receiptNumber: "MK-1", items: [{ orderLineId: "ln1", deliveredQuantity: 6, varianceReason: "Ambalaj hasarlı" }], isManagerApproved: true, notes: "",
    });
  });

  it("a new order takes the item's own unit and refuses an empty form without calling the server", async () => {
    const client = fakeClient();
    await render(<PurchasingSection capabilities={none} client={client} />);
    await submit("Yeni satın alma siparişi");
    expect(alertText()).toBe("En az bir sipariş kalemi girin.");
    await type("o-number", "PO-9");
    await type("o-supplier", "s1");
    await type("o-location", "l1");
    await type("l-item-0", "i1");
    await type("l-qty-0", "3,5");
    await type("l-price-0", "12");
    await submit("Yeni satın alma siparişi");
    expect(client.createOrder).toHaveBeenCalledWith({
      orderNumber: "PO-9", supplierId: "s1", destinationLocationId: "l1", lines: [{ stockItemId: "i1", orderedQuantity: 3.5, unitCode: "kg", unitPrice: 12 }], notes: "",
    });
  });

  it("the server's Turkish reason is shown as is", async () => {
    const client = fakeClient("Draft", { submitOrder: vi.fn().mockRejectedValue(new ManagementApiError(409, "X", "Sipariş kalemsiz gönderilemez.")) });
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Gönder");
    expect(alertText()).toBe("Sipariş kalemsiz gönderilemez.");
  });

  it("a supplier can be deactivated", async () => {
    const client = fakeClient();
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Pasifleştir");
    expect(client.setSupplierActive).toHaveBeenCalledWith("s1", false);
  });
  it("edits a supplier from the server's current detail and sends blanks as null", async () => {
    const client = fakeClient();
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Düzenle");
    expect(client.getSupplier).toHaveBeenCalledWith("s1");
    expect((document.getElementById("e-tax") as HTMLInputElement).value).toBe("1234567890");
    await type("e-name", "Un Fabrikası A.Ş.");
    await type("e-phone", "");
    await submit("Tedarikçiyi düzenle");
    expect(client.updateSupplier).toHaveBeenCalledWith("s1", { name: "Un Fabrikası A.Ş.", taxNumber: "1234567890", taxOffice: "Kadıköy", phone: null, email: "ted@example.com" });
    expect(document.body.textContent).toContain("Tedarikçi güncellendi.");
  });

  it("refuses to edit when the server returned masked supplier data, so a mask is never written back", async () => {
    const client = fakeClient("Submitted", {
      getSupplier: vi.fn().mockResolvedValue({ id: "s1", code: "TED-1", name: "Un Fabrikası", taxNumber: "******7890", taxOffice: null, phone: null, email: null, active: true, isMasked: true }),
    });
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Düzenle");
    expect(alertText()).toBe("Bu oturumun yetkisi tedarikçi bilgilerini düzenlemeye yetmiyor.");
    expect(document.querySelector('form[aria-label="Tedarikçiyi düzenle"]')).toBeNull();
    expect(client.updateSupplier).not.toHaveBeenCalled();
  });

  it("requires a supplier name and shows the server's Turkish refusal on update", async () => {
    const client = fakeClient("Submitted", { updateSupplier: vi.fn().mockRejectedValue(new ManagementApiError(409, "DUPLICATE_TAX_NUMBER", "Bu vergi numarası başka tedarikçide kayıtlı.")) });
    await render(<PurchasingSection capabilities={none} client={client} />);
    await press("Düzenle");
    await type("e-name", " ");
    await submit("Tedarikçiyi düzenle");
    expect(alertText()).toBe("Tedarikçi adı gerekli.");
    expect(client.updateSupplier).not.toHaveBeenCalled();
    await type("e-name", "Yeni ad");
    await submit("Tedarikçiyi düzenle");
    expect(alertText()).toBe("Bu vergi numarası başka tedarikçide kayıtlı.");
  });

  it("the client reads the supplier detail and updates it on the supplier route", async () => {
    const fetcher = vi.fn((_url: string, _init?: RequestInit) => Promise.resolve(new Response("{}", { status: 200 })));
    const api = createPurchasingClient(fetcher as unknown as typeof fetch);
    await api.getSupplier("s1");
    expect(fetcher.mock.calls.at(-1)![0]).toBe("/api/v1/management/purchasing/suppliers/s1");
    await api.updateSupplier("s1", { name: "A", taxNumber: null, taxOffice: null, phone: null, email: null });
    const [url, init] = fetcher.mock.calls.at(-1)!;
    expect(url).toBe("/api/v1/management/purchasing/suppliers/s1");
    expect(init?.method).toBe("PUT");
    expect(JSON.parse(init!.body as string)).toEqual({ name: "A", taxNumber: null, taxOffice: null, phone: null, email: null });
  });
});
