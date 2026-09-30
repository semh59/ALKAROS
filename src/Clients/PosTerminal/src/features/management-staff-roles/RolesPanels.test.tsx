// @vitest-environment jsdom

import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { type StaffClient } from "./api";
import { permissionLabel } from "./models";
import { StaffSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const codes = [
  "bills.comp", "bills.discount", "bills.split", "bills.void", "cash.drawer", "cash.session.override", "catalog.manage", "floorplan.manage",
  "identity.device_sessions.manage", "identity.permissions.manage", "identity.roles.manage", "identity.users.manage", "integrations.manage",
  "inventory.manage", "kitchen.advance", "kitchen.availability.suspend", "kitchen.reprint", "kitchen.routing.manage", "menu.manage",
  "observability.manage", "operations.backup", "orders.create", "orders.send", "orders.transfer-server", "orders.transfer-server-any",
  "payments.take", "production.manage", "purchasing.manage", "reconciliation.manage", "reports.close-day", "reports.view", "security.manage",
  "settings.manage", "tables.merge", "tables.reserve", "tables.status", "tables.transfer",
];

const roles = [
  { roleId: "r-manager", code: "manager", name: "Manager", permissionCodes: ["reports.view", "menu.manage"] },
  { roleId: "r-salon", code: "salon", name: "Salon sorumlusu", permissionCodes: [] },
];
const users = [
  { userId: "u1", username: "ayse", displayName: "Ayşe Yılmaz", active: true, roleIds: ["r-manager", "r-gone"] },
  { userId: "u2", username: "veli", displayName: "Veli Kaya", active: false, roleIds: [] },
];

function fakeClient(overrides: Partial<StaffClient> = {}): StaffClient {
  return {
    createUser: vi.fn().mockResolvedValue(undefined),
    lookupUser: vi.fn().mockResolvedValue(null),
    setActive: vi.fn().mockResolvedValue(undefined),
    listRoles: vi.fn().mockResolvedValue(roles),
    listPermissions: vi.fn().mockResolvedValue(codes.map((code) => ({ code, name: `English ${code}` }))),
    listUsers: vi.fn().mockResolvedValue(users),
    createRole: vi.fn().mockResolvedValue(undefined),
    assignPermission: vi.fn().mockResolvedValue(undefined),
    revokePermission: vi.fn().mockResolvedValue(undefined),
    assignUser: vi.fn().mockResolvedValue(undefined),
    revokeUser: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

const caps = new Set(["identity.users.manage", "identity.roles.manage"]);
const tick = () => act(async () => { await Promise.resolve(); });
const checkbox = (label: string) => [...document.querySelectorAll("label")].find((element) => element.textContent === label)!.querySelector("input")!;
const toggle = (label: string) => act(async () => checkbox(label).click());
const rowFor = (text: string) => [...document.querySelectorAll("tbody tr")].find((row) => row.textContent?.includes(text))!;
const pressIn = (row: Element, text: string) => act(async () => [...row.querySelectorAll("button")].find((button) => button.textContent === text)!.click());

describe("roles and permissions panels", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the role tools are absent without identity.roles.manage", async () => {
    await render(<StaffSection capabilities={new Set(["identity.users.manage"])} client={fakeClient()} />);
    expect(document.body.textContent).not.toContain("Roller ve izinler");
    expect(document.body.textContent).not.toContain("Personelin rolleri");
  });

  it("every permission the server ships has a Turkish name", () => {
    for (const code of codes) expect(permissionLabel(code), code).not.toBe("Tanımsız izin");
  });

  it("lists roles with Turkish names and never a raw code or English permission name", async () => {
    await render(<StaffSection capabilities={caps} client={fakeClient()} />);
    await tick();
    const text = document.body.textContent!;
    expect(text).toContain("Yönetici");
    expect(text).toContain("Salon sorumlusu");
    expect(text).not.toContain("Manager");
    expect(rowFor("Yönetici").textContent).toContain("2");
  });

  it("granting a permission is immediate and revoking asks first", async () => {
    const client = fakeClient();
    await render(<StaffSection capabilities={caps} client={client} />);
    await tick();
    await pressIn(rowFor("Salon sorumlusu"), "İzinleri düzenle");
    expect(document.body.textContent).toContain("Raporları görüntüleme");
    expect(document.body.textContent).not.toContain("English reports.view");
    await toggle("Raporları görüntüleme");
    expect(client.assignPermission).toHaveBeenCalledWith("r-salon", "reports.view");

    await pressIn(rowFor("Yönetici"), "İzinleri düzenle");
    expect(checkbox("Raporları görüntüleme").checked).toBe(true);
    await toggle("Raporları görüntüleme");
    expect(client.revokePermission).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain("Raporları görüntüleme: Bu izin geri alınsın mı?");
    await press("Vazgeç");
    expect(client.revokePermission).not.toHaveBeenCalled();
    await toggle("Raporları görüntüleme");
    await press("Evet, geri al");
    expect(client.revokePermission).toHaveBeenCalledWith("r-manager", "reports.view");
  });

  it("a new role needs a lowercase code and a name, then is created", async () => {
    const client = fakeClient();
    await render(<StaffSection capabilities={caps} client={client} />);
    await tick();
    await type("r-code", "Salon Sorumlusu");
    await type("r-name", "Salon");
    await submit("Yeni rol");
    expect(alertText()).toBe("Rol kodu (küçük harf, rakam, tire) ve rol adı girin.");
    expect(client.createRole).not.toHaveBeenCalled();
    await type("r-code", "salon-sorumlusu");
    await submit("Yeni rol");
    expect(client.createRole).toHaveBeenCalledWith("salon-sorumlusu", "Salon");
  });

  it("shows each person's roles by name and marks an unknown role", async () => {
    await render(<StaffSection capabilities={caps} client={fakeClient()} />);
    await tick();
    expect(rowFor("Ayşe Yılmaz").textContent).toContain("Yönetici, Tanımsız rol");
    expect(rowFor("Veli Kaya").textContent).toContain("Pasif");
  });

  it("assigns only a role the person does not have, and revoking asks first", async () => {
    const client = fakeClient();
    await render(<StaffSection capabilities={caps} client={client} />);
    await tick();
    await pressIn(rowFor("Ayşe Yılmaz"), "Rolleri düzenle");
    const options = [...document.querySelectorAll("#u-role option")].map((option) => option.textContent);
    expect(options).toEqual(["Seçin", "Salon sorumlusu"]);
    await type("u-role", "r-salon");
    await press("Rolü ata");
    expect(client.assignUser).toHaveBeenCalledWith("r-salon", "u1");

    await pressIn(document.querySelector('[role="group"][aria-label="Ayşe Yılmaz"]')!, "Rolü geri al");
    expect(client.revokeUser).not.toHaveBeenCalled();
    await press("Evet, geri al");
    expect(client.revokeUser).toHaveBeenCalledWith("r-manager", "u1");
  });

  it("shows the Turkish reason from the server as is", async () => {
    const client = fakeClient({ assignPermission: vi.fn().mockRejectedValue(new ManagementApiError(409, "X", "Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı.")) });
    await render(<StaffSection capabilities={caps} client={client} />);
    await tick();
    await pressIn(rowFor("Salon sorumlusu"), "İzinleri düzenle");
    await toggle("Raporları görüntüleme");
    expect(alertText()).toBe("Bu rol, yetki ya da kullanıcı adı zaten var veya belirtilen yetki bulunamadı.");
  });

  it("a role created in the roles panel is at once selectable in the people panel", async () => {
    const created = { roleId: "r-new", code: "yeni", name: "Yeni rol adı", permissionCodes: [] };
    const listRoles = vi.fn().mockImplementation(async () => (createRole.mock.calls.length > 0 ? [...roles, created] : roles));
    const createRole = vi.fn().mockResolvedValue(undefined);
    await render(<StaffSection capabilities={caps} client={fakeClient({ listRoles, createRole })} />);
    await tick();
    await type("r-code", "yeni");
    await type("r-name", "Yeni rol adı");
    await submit("Yeni rol");
    await tick();
    await pressIn(rowFor("Veli Kaya"), "Rolleri düzenle");
    const options = [...document.querySelectorAll("#u-role option")].map((option) => option.textContent);
    expect(options).toContain("Yeni rol adı");
  });

  it("the section offers no raw permission code anywhere", async () => {
    await render(<StaffSection capabilities={caps} client={fakeClient()} />);
    await tick();
    await pressIn(rowFor("Yönetici"), "İzinleri düzenle");
    expect(buttons()).toContain("Rolü oluştur");
    expect(document.body.textContent).not.toMatch(/\b[a-z]+\.[a-z-]+\b/);
  });
});
