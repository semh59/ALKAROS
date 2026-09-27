// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineProblemsTab } from "./OnlineProblemsTab";
import type { OnlineProblem } from "./onlineProblemsApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const problems: OnlineProblem[] = [
  { caseId: "c-1", kind: "ProviderAcceptedLocallyRefused", provider: "trendyol-go", externalOrderId: "pkg-1", amount: 0, severity: "High",
    status: "Open", openedAt: "2026-09-27T10:00:00Z", rowVersion: 3, nextAction: "ReprocessProviderEvent", canRetry: true },
  { caseId: "c-2", kind: "SomethingNew", provider: null, externalOrderId: null, amount: 125.5, severity: "Medium",
    status: "Escalated", openedAt: "2026-09-27T09:00:00Z", rowVersion: 1, nextAction: "Unheard", canRetry: false },
];

describe("OnlineProblemsTab", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => (init?.method === "POST" ? ok({ outcome: "Requeued" }) : ok(problems)));
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render(canAct: boolean) {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlineProblemsTab terminalId="t-1" canAct={canAct} />); });
  }

  const button = (text: string) => [...document.querySelectorAll("button")].filter((b) => b.textContent === text);
  const posts = () => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "POST");

  it("lists every open problem in Turkish and lets a viewer only read", async () => {
    await render(false);
    const text = document.body.textContent ?? "";
    expect(text).toContain("Platformun kabul ettiği sipariş burada oluşturulamadı");
    expect(text).toContain("Trendyol Go");
    expect(text).toContain("Platform sipariş no: pkg-1");
    expect(text).toContain("Önerilen: Ürün eşlemesini düzeltip olayı yeniden işleyin");
    expect(text).toContain("Online sipariş sorunu");
    expect(text).toContain("Platform belirsiz");
    expect(text).toContain("Üst yönetimde");
    expect(text).toContain("Önerilen: Önerilen eylemi uygulayın");
    for (const raw of ["ProviderAcceptedLocallyRefused", "SomethingNew", "Escalated", "trendyol-go", "Unheard"]) expect(text).not.toContain(raw);
    expect(button("Yeniden dene")).toHaveLength(0);
    expect(button("Çözüldü")).toHaveLength(0);

    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("offers retry only where it is safe and closes a problem only with a note", async () => {
    await render(true);
    expect(button("Yeniden dene")).toHaveLength(1);
    await act(async () => { button("Yeniden dene")[0].click(); });
    expect((posts()[0] as [string])[0]).toBe("/api/v1/terminals/t-1/online-problems/c-1/retry");
    expect(document.body.textContent).toContain("Yeniden denenmek üzere sıraya alındı.");

    await act(async () => { button("Çözüldü")[0].click(); });
    const save = button("Kaydet")[0];
    expect(save.disabled).toBe(true);
    const textarea = document.querySelector("textarea") as HTMLTextAreaElement;
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")!.set!.call(textarea, "  Eşleme düzeltildi.  ");
    await act(async () => { textarea.dispatchEvent(new Event("input", { bubbles: true })); });
    await act(async () => { button("Kaydet")[0].click(); });
    const [url, init] = posts()[1] as [string, RequestInit];
    expect(url).toBe("/api/v1/terminals/t-1/online-problems/c-1/resolve");
    expect(JSON.parse(init.body as string)).toEqual({ expectedVersion: 3, note: "Eşleme düzeltildi." });
    expect(document.body.textContent).toContain("Sorun kapatıldı.");
  });

  it("shows why closing was refused in the server's Turkish", async () => {
    await render(true);
    fetchMock.mockImplementationOnce(async () => ({ ok: false, status: 409,
      json: async () => ({ error: { code: "SOURCE_STILL_DIVERGED", message: "Sorun henüz ortadan kalkmadı; önce önerilen eylemi uygulayın." } }) }));
    await act(async () => { button("Çözüldü")[1].click(); });
    const textarea = document.querySelector("textarea") as HTMLTextAreaElement;
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")!.set!.call(textarea, "x");
    await act(async () => { textarea.dispatchEvent(new Event("input", { bubbles: true })); });
    await act(async () => { button("Kaydet")[0].click(); });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Sorun henüz ortadan kalkmadı; önce önerilen eylemi uygulayın.");
    expect(document.body.textContent).not.toContain("SOURCE_STILL_DIVERGED");
  });

  it("says when there is nothing to fix", async () => {
    fetchMock.mockImplementation(async () => ok([]));
    await render(true);
    expect(document.body.textContent).toContain("Açık online sipariş sorunu yok.");
  });
});
