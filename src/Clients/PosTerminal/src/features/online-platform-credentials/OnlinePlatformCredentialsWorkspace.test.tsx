// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlinePlatformCredentialsWorkspace } from "./OnlinePlatformCredentialsWorkspace";
import { describeChange, type OnlinePlatformCredentials } from "./onlinePlatformCredentialsApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const yemeksepeti: OnlinePlatformCredentials = {
  provider: "yemeksepeti",
  updatedAt: "2026-09-26T10:00:00Z",
  fields: [
    { name: "api-base-url", isSecret: false, configured: true, value: "https://partner.example.test" },
    { name: "chain-id", isSecret: false, configured: true, value: "chain-1" },
    { name: "vendor-id", isSecret: false, configured: false, value: null },
    { name: "client-id", isSecret: false, configured: false, value: null },
    { name: "client-secret", isSecret: true, configured: true, value: null },
    { name: "webhook-secret", isSecret: true, configured: false, value: null },
  ],
};

const trendyol: OnlinePlatformCredentials = {
  provider: "trendyol-go",
  updatedAt: null,
  fields: [
    { name: "api-base-url", isSecret: false, configured: false, value: null },
    { name: "supplier-id", isSecret: false, configured: false, value: null },
    { name: "api-key", isSecret: true, configured: false, value: null },
    { name: "api-secret", isSecret: true, configured: false, value: null },
  ],
};

describe("OnlinePlatformCredentialsWorkspace", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });
  const failed = (status: number, body: unknown) => ({ ok: false, status, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render(platforms: OnlinePlatformCredentials[] = [yemeksepeti, trendyol]) {
    fetchMock.mockResolvedValueOnce(ok({ platforms }));
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlinePlatformCredentialsWorkspace terminalId="t-1" />); });
  }

  const form = (name: string) => document.querySelector(`form[aria-label="${name} bağlantı bilgileri"]`) as HTMLFormElement;
  const input = (scope: HTMLElement, label: string) => {
    const element = [...scope.querySelectorAll("label")].find((l) => l.textContent === label)!;
    return document.getElementById(element.htmlFor) as HTMLInputElement;
  };
  const button = (scope: HTMLElement, text: string) =>
    [...scope.querySelectorAll("button")].find((b) => b.textContent === text) as HTMLButtonElement;
  const type = async (element: HTMLInputElement, value: string) => {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
    await act(async () => {
      setter.call(element, value);
      element.dispatchEvent(new Event("input", { bubbles: true }));
    });
  };
  const puts = () => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "PUT");

  it("lists every platform's fields in Turkish, shows stored plain values and only whether a secret is set", async () => {
    await render();

    const text = document.body.textContent ?? "";
    expect(text).toContain("Yemeksepeti");
    expect(text).toContain("Trendyol Go");
    expect(text).toContain("Henüz bilgi kaydedilmedi.");
    expect(text).toContain("doğrulanmamış taslak");
    for (const raw of ["client-secret", "api-base-url", "trendyol-go", "yemeksepeti"]) expect(text).not.toContain(raw);

    const ysp = form("Yemeksepeti");
    expect(input(ysp, "API adresi").value).toBe("https://partner.example.test");
    expect(input(ysp, "Zincir kimliği").value).toBe("chain-1");
    const secret = input(ysp, "İstemci gizli anahtarı");
    expect(secret.type).toBe("password");
    expect(secret.value).toBe("");
    expect(secret.placeholder).toBe("Değiştirmek için yeni değer girin");
    expect(input(ysp, "Sipariş bildirimi doğrulama anahtarı").placeholder).toBe("");
    expect(ysp.textContent).toContain("Kayıtlı değil");
    expect(button(ysp, "Kaydet").disabled).toBe(true);
    // Only a stored field can be removed.
    expect([...ysp.querySelectorAll("button")].filter((b) => b.textContent === "Kaldır")).toHaveLength(3);

    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("sends only what changed, then shows the saved state and forgets the typed secret", async () => {
    await render();
    const ysp = form("Yemeksepeti");
    await type(input(ysp, "Zincir kimliği"), " chain-2 ");
    await type(input(ysp, "İstemci gizli anahtarı"), "new-secret");

    const saved: OnlinePlatformCredentials = {
      ...yemeksepeti,
      updatedAt: "2026-09-26T11:00:00Z",
      fields: yemeksepeti.fields.map((f) => (f.name === "chain-id" ? { ...f, value: "chain-2" } : f)),
    };
    fetchMock.mockResolvedValueOnce(ok(saved));
    await act(async () => { button(ysp, "Kaydet").click(); });

    expect(puts()).toHaveLength(1);
    const [url, init] = puts()[0] as [string, RequestInit];
    expect(url).toBe("/api/v1/terminals/t-1/online-platform-credentials/yemeksepeti");
    expect(JSON.parse(init.body as string)).toEqual({ values: { "chain-id": "chain-2", "client-secret": "new-secret" }, cleared: [] });
    expect(input(ysp, "Zincir kimliği").value).toBe("chain-2");
    expect(input(ysp, "İstemci gizli anahtarı").value).toBe("");
    expect(ysp.textContent).toContain("Yemeksepeti bilgileri kaydedildi.");
    expect(ysp.textContent).not.toContain("new-secret");
    expect(button(ysp, "Kaydet").disabled).toBe(true);
  });

  it("removes an emptied plain field and a secret marked for removal, and a removal can be undone", async () => {
    await render();
    const ysp = form("Yemeksepeti");
    await type(input(ysp, "Zincir kimliği"), "");
    const removeSecret = ysp.querySelector('button[aria-label="İstemci gizli anahtarı alanını kaldır"]') as HTMLButtonElement;
    await act(async () => { removeSecret.click(); });
    expect(input(ysp, "İstemci gizli anahtarı").disabled).toBe(true);
    expect(ysp.textContent).toContain("Kaydedince silinecek");

    const removeUrl = ysp.querySelector('button[aria-label="API adresi alanını kaldır"]') as HTMLButtonElement;
    await act(async () => { removeUrl.click(); });
    await act(async () => { (ysp.querySelector('button[aria-label="API adresi alanını silmekten vazgeç"]') as HTMLButtonElement).click(); });
    expect(input(ysp, "API adresi").disabled).toBe(false);

    fetchMock.mockResolvedValueOnce(ok(yemeksepeti));
    await act(async () => { button(ysp, "Kaydet").click(); });
    expect(JSON.parse((puts()[0] as [string, RequestInit])[1].body as string)).toEqual({ values: {}, cleared: ["chain-id", "client-secret"] });
  });

  it("names the refused field in Turkish and keeps what was typed", async () => {
    await render();
    const tgo = form("Trendyol Go");
    await type(input(tgo, "API adresi"), "http://stageapi.example.test");
    fetchMock.mockResolvedValueOnce(failed(400, {
      error: { code: "VALIDATION_FAILED", message: "Adres https:// ile başlayan geçerli bir adres olmalıdır.", field: "api-base-url" },
    }));
    await act(async () => { button(tgo, "Kaydet").click(); });

    expect(tgo.querySelector('[role="alert"]')?.textContent).toBe("API adresi: Adres https:// ile başlayan geçerli bir adres olmalıdır.");
    expect(input(tgo, "API adresi").value).toBe("http://stageapi.example.test");
    expect(document.body.textContent).not.toContain("VALIDATION_FAILED");
  });

  it("never shows a raw status or English text when the server refuses or cannot be reached", async () => {
    await render();
    const ysp = form("Yemeksepeti");
    await type(input(ysp, "Restoran kimliği"), "v-1");
    fetchMock.mockResolvedValueOnce(failed(403, { error: { code: "FORBIDDEN", message: "Forbidden" } }));
    await act(async () => { button(ysp, "Kaydet").click(); });
    expect(ysp.querySelector('[role="alert"]')?.textContent).toBe("Bu işlem için yetkiniz yok.");

    fetchMock.mockResolvedValueOnce(failed(500, {}));
    await act(async () => { button(ysp, "Kaydet").click(); });
    expect(ysp.querySelector('[role="alert"]')?.textContent).toBe("Platform bilgileri kaydedilemedi.");

    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch"));
    await act(async () => { button(ysp, "Kaydet").click(); });
    expect(ysp.querySelector('[role="alert"]')?.textContent).toBe("Sunucuya ulaşılamadı. Tekrar deneyin.");
  });

  it("explains a failed load and never shows an unknown platform or field by its id", async () => {
    fetchMock.mockResolvedValueOnce(failed(503, { error: { code: "DATABASE_UNAVAILABLE", message: "Veritabanı işlemi tamamlanamadı." } }));
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlinePlatformCredentialsWorkspace terminalId="t-1" />); });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Veritabanı işlemi tamamlanamadı.");
    act(() => root?.unmount());

    await render([{ provider: "future-platform", updatedAt: null, fields: [{ name: "future-field", isSecret: false, configured: false, value: null }] }]);
    const text = document.body.textContent ?? "";
    expect(text).toContain("Diğer platform");
    expect(text).toContain("Ek alan");
    expect(text).not.toContain("future");
  });
});

describe("describeChange", () => {
  it("leaves untouched fields out and never clears a field that is not stored", () => {
    expect(describeChange(yemeksepeti, {}, new Set())).toEqual({ values: {}, cleared: [] });
    expect(describeChange(yemeksepeti, { "chain-id": "chain-1", "client-secret": "  " }, new Set())).toEqual({ values: {}, cleared: [] });
    expect(describeChange(yemeksepeti, { "vendor-id": "" }, new Set(["webhook-secret"]))).toEqual({ values: {}, cleared: [] });
    expect(describeChange(yemeksepeti, { "client-secret": "typed" }, new Set(["client-secret"]))).toEqual({ values: {}, cleared: ["client-secret"] });
  });
});
