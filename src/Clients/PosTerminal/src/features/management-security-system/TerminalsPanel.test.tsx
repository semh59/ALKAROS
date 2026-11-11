// @vitest-environment jsdom

import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createSecurityClient, type SecurityClient } from "./api";
import { SecuritySection } from "./SecuritySection";
import { TerminalsPanel } from "./TerminalsPanel";
import { alertText, press, render, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const bound = { terminalId: "aaaaaaaa-1111-2222-3333-444444444444", bound: true, createdAt: "2026-10-01T08:00:00Z", lastUserName: "Ayşe Kasiyer", lastLoginAt: "2026-10-23T09:00:00Z", name: "Kasa 1" };
const open = { terminalId: "bbbbbbbb-1111-2222-3333-444444444444", bound: false, createdAt: "2026-10-02T08:00:00Z", lastUserName: null, lastLoginAt: null, name: null };

function fakeClient(overrides: Partial<SecurityClient> = {}): SecurityClient {
  return {
    listTerminals: vi.fn().mockResolvedValue([bound, open]),
    resetTerminal: vi.fn().mockResolvedValue(undefined),
    renameTerminal: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as SecurityClient;
}

describe("terminals panel", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("lists each terminal with its last user and binding state in Turkish", async () => {
    await render(<TerminalsPanel client={fakeClient()} />);
    expect(document.body.textContent).toContain("bbbbbbbb");
    expect(document.body.textContent).toContain("Ayşe Kasiyer");
    expect(document.body.textContent).toContain("Tarayıcıya bağlı");
    expect(document.body.textContent).toContain("Bağlı değil");
  });

  it("gives every column header, including the action column, an accessible name", async () => {
    await render(<TerminalsPanel client={fakeClient()} />);
    const names = [...document.querySelectorAll("th")].map((header) => header.textContent);
    expect(names).toEqual(["Terminal", "Ad", "Son giriş yapan", "Son giriş", "Durum", "Bağlantıyı kaldır"]);
    expect((await axe.run(document.body)).violations).toEqual([]);
  });

  it("shows the name where there is one, the short id where there is not, and saves a new name then reloads", async () => {
    const client = fakeClient();
    await render(<TerminalsPanel client={client} />);
    const cells = [...document.querySelectorAll("tbody tr")].map((row) => row.querySelector("td")!.textContent);
    expect(cells).toEqual(["Kasa 1", "bbbbbbbb"]);
    expect((document.getElementById(`terminal-name-${bound.terminalId}`) as HTMLInputElement).value).toBe("Kasa 1");
    await type(`terminal-name-${bound.terminalId}`, "Kasa 3");
    await press("Adı kaydet");
    expect(client.renameTerminal).toHaveBeenCalledTimes(1);
    expect(client.renameTerminal).toHaveBeenCalledWith(bound.terminalId, "Kasa 3");
    expect(client.listTerminals).toHaveBeenCalledTimes(2);
    expect(document.body.textContent).toContain("Terminal adı kaydedildi.");
  });

  it("keeps the save button off until the name changes, names the terminal on it and lets a name be cleared", async () => {
    const client = fakeClient();
    await render(<TerminalsPanel client={client} />);
    const save = () => [...document.querySelectorAll("button")].filter((button) => button.textContent === "Adı kaydet") as HTMLButtonElement[];
    expect(save().map((button) => button.disabled)).toEqual([true, true]);
    expect(save().map((button) => button.getAttribute("aria-label"))).toEqual(["Kasa 1 terminalinin adını kaydet", "bbbbbbbb terminalinin adını kaydet"]);
    await type(`terminal-name-${bound.terminalId}`, "");
    await press("Adı kaydet");
    expect(client.renameTerminal).toHaveBeenCalledWith(bound.terminalId, "");
  });

  it("shows the server's Turkish reason when the name is taken", async () => {
    await render(<TerminalsPanel client={fakeClient({ renameTerminal: vi.fn().mockRejectedValue(new ManagementApiError(409, "NAME_TAKEN", "Bu ad başka bir terminalde kullanılıyor.")) })} />);
    await type(`terminal-name-${bound.terminalId}`, "Kasa 9");
    await press("Adı kaydet");
    expect(alertText()).toBe("Bu ad başka bir terminalde kullanılıyor.");
  });

  it("releases a bound terminal and reloads the list; an unbound one has nothing to release", async () => {
    const client = fakeClient();
    await render(<TerminalsPanel client={client} />);
    const buttons = [...document.querySelectorAll("button")].filter((button) => button.textContent === "Bağlantıyı kaldır") as HTMLButtonElement[];
    expect(buttons.map((button) => button.disabled)).toEqual([false, true]);
    expect(buttons[0].getAttribute("aria-label")).toBe("Kasa 1 terminalinin tarayıcı bağlantısını kaldır");
    await press("Bağlantıyı kaldır");
    expect(client.resetTerminal).toHaveBeenCalledWith(bound.terminalId);
    expect(client.listTerminals).toHaveBeenCalledTimes(2);
    expect(document.body.textContent).toContain("Terminalin bağlantısı kaldırıldı");
  });

  it("shows the server's Turkish message when the release fails and a generic one otherwise", async () => {
    await render(<TerminalsPanel client={fakeClient({ resetTerminal: vi.fn().mockRejectedValue(new ManagementApiError(404, "NOT_FOUND", "İstenen terminal bulunamadı.")) })} />);
    await press("Bağlantıyı kaldır");
    expect(alertText()).toBe("İstenen terminal bulunamadı.");
    await unmount();
    await render(<TerminalsPanel client={fakeClient({ resetTerminal: vi.fn().mockRejectedValue(new Error("boom")) })} />);
    await press("Bağlantıyı kaldır");
    expect(alertText()).toBe("İşlem tamamlanamadı.");
  });

  it("says so when there is no terminal yet", async () => {
    await render(<TerminalsPanel client={fakeClient({ listTerminals: vi.fn().mockResolvedValue([]) })} />);
    expect(document.body.textContent).toContain("Henüz kayıtlı terminal yok.");
  });
  it("calls the terminal list and release routes of the security API", async () => {
    const fetcher = vi.fn().mockImplementation(async () => new Response(JSON.stringify([bound]), { status: 200 }));
    const api = createSecurityClient(fetcher as unknown as typeof fetch);
    expect(await api.listTerminals()).toEqual([bound]);
    await api.resetTerminal(bound.terminalId);
    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/management/security/terminals");
    expect(fetcher.mock.calls[1][0]).toBe(`/api/v1/management/security/terminals/${bound.terminalId}/reset-binding`);
    expect(fetcher.mock.calls[1][1].method).toBe("POST");
    await api.renameTerminal(bound.terminalId, "Kasa 1");
    expect(fetcher.mock.calls[2][0]).toBe(`/api/v1/management/security/terminals/${bound.terminalId}/name`);
    expect(fetcher.mock.calls[2][1].method).toBe("PUT");
    expect(JSON.parse(fetcher.mock.calls[2][1].body)).toEqual({ name: "Kasa 1" });
  });

  it("is part of the security section", async () => {
    const client = fakeClient({
      lookupUser: vi.fn(), listJobs: vi.fn().mockResolvedValue([]), backupRpo: vi.fn().mockResolvedValue([]),
      restoreAttempts: vi.fn().mockResolvedValue([]), hangingOrders: vi.fn().mockResolvedValue([]), orderBacklog: vi.fn().mockResolvedValue({ liveOrders: 0, provablySettled: 0, withoutBill: 0, withOpenBill: 0, oldestSettledCreatedAt: null }),
    });
    await render(<SecuritySection client={client} />);
    expect(client.listTerminals).toHaveBeenCalled();
    expect(document.body.textContent).toContain("Terminaller");
  });
});
