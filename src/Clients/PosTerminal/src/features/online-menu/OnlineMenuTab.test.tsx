// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineMenuTab } from "./OnlineMenuTab";
import type { OnlineMenu } from "./onlineMenuApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const channels = { platforms: [
  { provider: "trendyol-go", displayName: "Trendyol Go", configured: true, lastEventAt: null, polling: "NotPolled" },
  { provider: "yemeksepeti", displayName: "Yemeksepeti", configured: true, lastEventAt: null, polling: "NotPolled" },
] };

const tgoMenu: OnlineMenu = {
  provider: "trendyol-go",
  channel: "Trendyol Go",
  products: [
    { productId: "p-1", name: "Adana Kebap", catalogSku: "AK-1", catalogPrice: 250, active: true, externalSku: "11", publishedPrice: 240, saleState: "OnSale" },
    { productId: "p-2", name: "Mercimek Çorbası", catalogSku: "MC-1", catalogPrice: 90, active: true, externalSku: null, publishedPrice: null, saleState: "Unknown" },
    { productId: "p-3", name: "Ayran", catalogSku: "AY-1", catalogPrice: 30, active: true, externalSku: "99", publishedPrice: 30, saleState: "SoldOut" },
  ],
  platformProducts: [
    { id: "11", name: "Adana Kebap (TGO)", active: true, mappedProductId: "p-1" },
    { id: "12", name: "Çorba (TGO)", active: true, mappedProductId: null },
  ],
  platformMenuUnavailable: false,
  menus: [{ menuId: "m-1", name: "Ana menü" }],
  publications: [
    { publicationId: "pub-1", status: "Delivered", requestedAt: "2026-09-27T10:00:00Z", itemCount: 2, validationErrorCount: 1, hasError: false, deliveredAt: "2026-09-27T10:00:05Z" },
    { publicationId: "pub-2", status: "Pending", requestedAt: "2026-09-27T09:00:00Z", itemCount: 2, validationErrorCount: 0, hasError: true, deliveredAt: null },
  ],
};

const yspMenu: OnlineMenu = { ...tgoMenu, provider: "yemeksepeti", channel: "Yemeksepeti", platformProducts: null, publications: [],
  products: tgoMenu.products.map((p) => ({ ...p, externalSku: p.productId === "p-1" ? "ys-11" : null })) };

describe("OnlineMenuTab", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });
  const failed = (status: number, body: unknown) => ({ ok: false, status, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url.endsWith("/online-channels")) return ok(channels);
      if (url.endsWith("/online-menu/trendyol-go")) return ok(tgoMenu);
      if (url.endsWith("/online-menu/yemeksepeti")) return ok(yspMenu);
      if (init?.method === "POST") return ok({ status: "Pending", itemCount: 2, validationErrors: [{ productId: "p-2", code: "ExternalIdUnavailable" }] });
      return { ok: true, status: 204, json: async () => ({}) };
    });
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render() {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlineMenuTab terminalId="t-1" />); });
    await act(async () => { await Promise.resolve(); });
  }

  const button = (text: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === text) as HTMLButtonElement;
  const labelled = (label: string) => document.querySelector(`[aria-label="${label}"]`) as HTMLElement;
  const calls = (method: string) => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === method);
  const choose = async (element: HTMLSelectElement | HTMLInputElement, value: string) => {
    const proto = element instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value")!.set!.call(element, value);
    await act(async () => { element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true })); });
  };

  it("shows each product's platform code, sale state and prices in Turkish with the platform's own names", async () => {
    await render();
    const text = document.body.textContent ?? "";
    expect(text).toContain("Adana Kebap (TGO)");
    expect(text).toContain("Satışta");
    expect(text).toContain("Satışta değil");
    expect(text).toContain("Henüz bildirilmedi");
    expect(text).toContain("(farklı)");
    expect(text).toContain("Yayınlandı, 2 ürün, 1 ürün yayınlanamadı");
    expect(text).toContain("Gönderilemedi, yeniden denenecek");
    expect(text).toContain("Platformda eşlenmemiş 1 ürün var: Çorba (TGO)");
    expect(text).toContain("Platform menüsünde bulunmayan eşleme: Ayran");
    for (const raw of ["OnSale", "SoldOut", "Delivered", "Pending", "trendyol-go"]) expect(text).not.toContain(raw);

    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("maps from the platform menu on Trendyol Go and removes a mapping", async () => {
    await render();
    await choose(labelled("Mercimek Çorbası için platform ürünü") as HTMLSelectElement, "12");
    await act(async () => { labelled("Mercimek Çorbası ürününü eşle").click(); });
    const [url, init] = calls("PUT")[0] as [string, RequestInit];
    expect(url).toBe("/api/v1/terminals/t-1/online-menu/trendyol-go/mappings/p-2");
    expect(JSON.parse(init.body as string)).toEqual({ externalSku: "12" });
    expect(document.body.textContent).toContain("Mercimek Çorbası eşlendi.");

    await act(async () => { labelled("Adana Kebap eşlemesini kaldır").click(); });
    expect((calls("DELETE")[0] as [string])[0]).toBe("/api/v1/terminals/t-1/online-menu/trendyol-go/mappings/p-1");
  });

  it("takes a typed code on Yemeksepeti and explains a refusal in the server's Turkish", async () => {
    await render();
    await act(async () => { button("Yemeksepeti").click(); });
    await act(async () => { await Promise.resolve(); });
    await act(async () => { labelled("Mercimek Çorbası ürününü eşle").click(); });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Önce platform ürününü seçin veya kodunu yazın.");
    expect(calls("PUT")).toHaveLength(0);

    fetchMock.mockImplementationOnce(async () => failed(409, { error: { code: "CODE_TAKEN", message: "Bu platform kodu başka bir ürüne eşli; önce o eşlemeyi kaldırın." } }));
    await choose(labelled("Mercimek Çorbası için platform ürün kodu") as HTMLInputElement, " ys-22 ");
    await act(async () => { labelled("Mercimek Çorbası ürününü eşle").click(); });
    expect(JSON.parse((calls("PUT")[0] as [string, RequestInit])[1].body as string)).toEqual({ externalSku: "ys-22" });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Bu platform kodu başka bir ürüne eşli; önce o eşlemeyi kaldırın.");
  });

  it("publishes the chosen catalog menu to the platform and names what could not be published", async () => {
    await render();
    await act(async () => { button("Trendyol Go menüsünü yayınla").click(); });
    const [url, init] = calls("POST")[0] as [string, RequestInit];
    expect(url).toBe("/api/v1/terminals/t-1/online-ordering/catalog-publications");
    expect(JSON.parse(init.body as string)).toEqual({ channel: "Trendyol Go", menuId: "m-1" });
    expect(document.body.textContent).toContain("Gönderiliyor — 2 ürün. 1 ürün yayınlanamadı: Mercimek Çorbası: platform kodu yok veya platform menüsünde değil");
  });

  it("finds products by name or code and can show only the unmapped ones", async () => {
    await render();
    await choose(document.querySelector('input[type="search"]') as HTMLInputElement, "çorba");
    expect(document.querySelectorAll("tbody tr")).toHaveLength(1);
    await choose(document.querySelector('input[type="search"]') as HTMLInputElement, "");
    await act(async () => { (document.querySelector('input[type="checkbox"]') as HTMLInputElement).click(); });
    expect([...document.querySelectorAll("tbody tr td:first-child")].map((c) => c.textContent)).toEqual(["Mercimek Çorbası"]);
  });

  it("says when the platform menu cannot be read so the code can be typed", async () => {
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/online-channels") ? ok({ platforms: [channels.platforms[0]] })
        : ok({ ...tgoMenu, platformProducts: null, platformMenuUnavailable: true }));
    await render();
    expect(document.body.textContent).toContain("Platform menüsü okunamadı; platform ürün kodunu elle yazabilirsiniz.");
    expect(labelled("Mercimek Çorbası için platform ürün kodu")).not.toBeNull();
    expect(document.querySelector('[aria-label="Platform seç"]')).toBeNull();
  });
});
