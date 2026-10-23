// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { createSecurityClient, type SecurityClient } from "./api";
import { SecuritySection } from "./SecuritySection";
import { TerminalsPanel } from "./TerminalsPanel";
import { alertText, press, render, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const bound = { terminalId: "aaaaaaaa-1111-2222-3333-444444444444", bound: true, createdAt: "2026-10-01T08:00:00Z", lastUserName: "Ayşe Kasiyer", lastLoginAt: "2026-10-23T09:00:00Z" };
const open = { terminalId: "bbbbbbbb-1111-2222-3333-444444444444", bound: false, createdAt: "2026-10-02T08:00:00Z", lastUserName: null, lastLoginAt: null };

function fakeClient(overrides: Partial<SecurityClient> = {}): SecurityClient {
  return {
    listTerminals: vi.fn().mockResolvedValue([bound, open]),
    resetTerminal: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as SecurityClient;
}

describe("terminals panel", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("lists each terminal with its last user and binding state in Turkish", async () => {
    await render(<TerminalsPanel client={fakeClient()} />);
    expect(document.body.textContent).toContain("aaaaaaaa");
    expect(document.body.textContent).toContain("Ayşe Kasiyer");
    expect(document.body.textContent).toContain("Tarayıcıya bağlı");
    expect(document.body.textContent).toContain("Bağlı değil");
  });

  it("releases a bound terminal and reloads the list; an unbound one has nothing to release", async () => {
    const client = fakeClient();
    await render(<TerminalsPanel client={client} />);
    const buttons = [...document.querySelectorAll("button")].filter((button) => button.textContent === "Bağlantıyı kaldır") as HTMLButtonElement[];
    expect(buttons.map((button) => button.disabled)).toEqual([false, true]);
    expect(buttons[0].getAttribute("aria-label")).toBe("aaaaaaaa terminalinin tarayıcı bağlantısını kaldır");
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
  });

  it("is part of the security section", async () => {
    const client = fakeClient({
      lookupUser: vi.fn(), listJobs: vi.fn().mockResolvedValue([]), backupRpo: vi.fn().mockResolvedValue([]),
      restoreAttempts: vi.fn().mockResolvedValue([]), orderBacklog: vi.fn().mockResolvedValue({ liveOrders: 0, provablySettled: 0, withoutBill: 0, withOpenBill: 0, oldestSettledCreatedAt: null }),
    });
    await render(<SecuritySection client={client} />);
    expect(client.listTerminals).toHaveBeenCalled();
    expect(document.body.textContent).toContain("Terminaller");
  });
});
