// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { SellerProfileCard } from "./SellerProfileCard";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const json = (body: unknown, status = 200) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: body === undefined ? {} : { "Content-Type": "application/json" } });

const stored = {
  legalName: "Deniz Lokantası Ltd. Şti.", taxIdKind: "Vkn", taxIdNumber: "1234567890", taxOffice: "Kadıköy",
  address: "Moda Cad. 1", district: "Kadıköy", city: "İstanbul", email: null,
};

describe("SellerProfileCard", () => {
  let root: Root | null = null;

  async function render(fetcher: typeof fetch) {
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<SellerProfileCard terminalId="t1" fetcher={fetcher} />));
    await act(async () => Promise.resolve());
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
  });

  const setValue = async (input: HTMLInputElement, value: string) => {
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")!.set!;
    await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
  };

  it("fills the form from the saved profile and says it is saved", async () => {
    await render((async () => json({ configured: true, profile: stored })) as unknown as typeof fetch);

    const inputs = document.querySelectorAll<HTMLInputElement>("form input");
    expect(inputs[0].value).toBe("Deniz Lokantası Ltd. Şti.");
    expect(document.body.textContent).toContain("Kayıtlı");
  });

  it("sends the edited profile with an empty e-mail as null and confirms in Turkish", async () => {
    const calls: { method?: string; body?: string; key?: string }[] = [];
    await render((async (_url: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === "PUT") { calls.push({ method: init.method, body: String(init.body), key: (init.headers as Record<string, string>)["X-Idempotency-Key"] }); return json(undefined, 204); }
      return json({ configured: true, profile: { ...stored, email: "muhasebe@deniz.example" } });
    }) as unknown as typeof fetch);

    expect(document.querySelector<HTMLInputElement>("input[type=email]")!.value).toBe("muhasebe@deniz.example");
    await setValue(document.querySelectorAll<HTMLInputElement>("form input")[0], "Yeni Ünvan");
    await setValue(document.querySelector<HTMLInputElement>("input[type=email]")!, "");
    await act(async () => document.querySelector<HTMLButtonElement>("form button")!.click());
    await act(async () => Promise.resolve());

    const sent = JSON.parse(calls[0].body!);
    expect(sent.legalName).toBe("Yeni Ünvan");
    expect(calls[0].key).toMatch(/^[0-9a-f-]{36}$/);
    expect(sent.email).toBeNull();
    expect(document.body.textContent).toContain("İşletme bilgileri kaydedildi.");
  });

  it("shows the server's Turkish refusal and never a status code or English text", async () => {
    await render((async (_url: RequestInfo | URL, init?: RequestInit) =>
      init?.method === "PUT"
        ? json({ error: { code: "VALIDATION_FAILED", message: "Şu alanlar eksik ya da hatalı: Vergi dairesi." } }, 400)
        : json({ configured: false, profile: null })) as unknown as typeof fetch);

    await act(async () => document.querySelector<HTMLButtonElement>("form button")!.click());
    await act(async () => Promise.resolve());

    expect(document.querySelector('[role="alert"]')!.textContent).toBe("Şu alanlar eksik ya da hatalı: Vergi dairesi.");
    expect(document.body.textContent).not.toContain("400");
    expect(document.body.textContent).not.toContain("kaydedildi");
  });

  it("falls back to a Turkish sentence when the server is unreachable", async () => {
    await render((async () => { throw new TypeError("Failed to fetch"); }) as unknown as typeof fetch);

    expect(document.querySelector('[role="alert"]')!.textContent).toBe("Sunucuya ulaşılamadı. Tekrar deneyin.");
  });
});
