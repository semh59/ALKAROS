// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import type { ReactElement } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { StaffApiError, createStaffClient, type StaffClient } from "./api";
import { StaffSection } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;
async function render(element: ReactElement) {
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => root!.render(element));
}
const buttons = () => [...document.querySelectorAll("button")].map((button) => button.textContent);
const press = (text: string) => act(async () => [...document.querySelectorAll("button")].find((button) => button.textContent === text)!.click());
const alertText = () => document.querySelector('[role="alert"]')?.textContent;
async function type(id: string, value: string) {
  const input = document.getElementById(id) as HTMLInputElement;
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}
const submit = (label: string) => act(async () => { document.querySelector(`form[aria-label="${label}"]`)!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });

const user = { userId: "u1", displayName: "Ayşe Yılmaz", active: true, isLocked: false };
const fakeClient = (overrides: Partial<StaffClient> = {}): StaffClient => ({
  createUser: vi.fn().mockResolvedValue(undefined),
  lookupUser: vi.fn().mockResolvedValue(user),
  setActive: vi.fn().mockResolvedValue(undefined),
  ...overrides,
});
const manager = new Set(["identity.users.manage", "security.manage"]);

describe("staff section", () => {
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("the shell lists the staff tab only for a session holding identity.users.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view", "menu.manage"])} />);
    expect(buttons()).not.toContain("Personel ve roller");
    await render(<ManagementArea capabilities={new Set(["reports.view", "identity.users.manage"])} />);
    expect(buttons()).toContain("Personel ve roller");
  });

  it("creates an account and clears the form", async () => {
    const client = fakeClient();
    await render(<StaffSection capabilities={manager} client={client} />);
    await type("u-username", "ayse");
    await type("u-display", "Ayşe Yılmaz");
    await type("u-password", "gizli-parola");
    await submit("Personel hesabı aç");
    expect(client.createUser).toHaveBeenCalledWith("ayse", "gizli-parola", "Ayşe Yılmaz");
    expect(document.body.textContent).toContain("Personel hesabı açıldı.");
    expect((document.getElementById("u-password") as HTMLInputElement).value).toBe("");
  });

  it("refuses a short password or a blank name without calling the server", async () => {
    const client = fakeClient();
    await render(<StaffSection capabilities={manager} client={client} />);
    await type("u-username", "ayse");
    await type("u-display", "Ayşe");
    await type("u-password", "kisa");
    await submit("Personel hesabı aç");
    expect(alertText()).toBe("Kullanıcı adı, görünen ad ve en az 8 karakterli bir parola girin.");
    expect(client.createUser).not.toHaveBeenCalled();
  });

  it("shows the server's Turkish reason when the account cannot be created", async () => {
    const client = fakeClient({ createUser: vi.fn().mockRejectedValue(new StaffApiError(409, "X", "Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı.")) });
    await render(<StaffSection capabilities={manager} client={client} />);
    await type("u-username", "ayse");
    await type("u-display", "Ayşe");
    await type("u-password", "gizli-parola");
    await submit("Personel hesabı aç");
    expect(alertText()).toBe("Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı.");
  });

  it("without security.manage the find and deactivate tools are absent", async () => {
    await render(<StaffSection capabilities={new Set(["identity.users.manage"])} client={fakeClient()} />);
    expect(document.querySelector('section[aria-label="Personel bul"]')).toBeNull();
    expect(buttons()).not.toContain("Pasifleştir");
  });

  it("finds a user and deactivates then reloads the state", async () => {
    const client = fakeClient({ lookupUser: vi.fn().mockResolvedValueOnce(user).mockResolvedValueOnce({ ...user, active: false }) });
    await render(<StaffSection capabilities={manager} client={client} />);
    await type("u-search", "ayse");
    await submit("Personel bul");
    expect(document.body.textContent).toContain("Ayşe Yılmaz · Etkin");
    await press("Pasifleştir");
    expect(client.setActive).toHaveBeenCalledWith("u1", false);
    expect(document.body.textContent).toContain("Ayşe Yılmaz · Pasif");
    expect(buttons()).toContain("Yeniden etkinleştir");
  });

  it("says so when no user has that name", async () => {
    await render(<StaffSection capabilities={manager} client={fakeClient({ lookupUser: vi.fn().mockResolvedValue(null) })} />);
    await type("u-search", "yok");
    await submit("Personel bul");
    expect(document.body.textContent).toContain("Bu kullanıcı adıyla personel bulunamadı.");
  });

  it("shows the server's Turkish reason when the deactivation is refused", async () => {
    const client = fakeClient({ setActive: vi.fn().mockRejectedValue(new StaffApiError(409, "SELF_DEACTIVATION", "Kendi hesabınızı pasifleştiremezsiniz.")) });
    await render(<StaffSection capabilities={manager} client={client} />);
    await type("u-search", "ayse");
    await submit("Personel bul");
    await press("Pasifleştir");
    expect(alertText()).toBe("Kendi hesabınızı pasifleştiremezsiniz.");
  });

  it("the client treats a 404 lookup as no user", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response(JSON.stringify({ error: { code: "NOT_FOUND", message: "yok" } }), { status: 404 })));
    expect(await createStaffClient(fetcher as unknown as typeof fetch).lookupUser("x")).toBeNull();
  });
});
