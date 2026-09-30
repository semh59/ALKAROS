// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { type SystemClient } from "./api";
import { SystemSection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import { ManagementApiError } from "../management/http";

const alert = { alertId: "a1", alertType: "x", severity: "Critical", status: "Open", title: "Yazıcı yanıt vermiyor", message: "Mutfak yazıcısına ulaşılamıyor.", openedAt: "2026-09-30T08:00:00Z", rowVersion: 7 };

function fakeClient(overrides: Partial<SystemClient> = {}): SystemClient {
  return {
    activeAlerts: vi.fn().mockResolvedValue([alert]),
    unhealthyChecks: vi.fn().mockResolvedValue([{ healthCheckId: "h1", checkType: "db", target: "veritabani", status: "Degraded", checkedAt: "2026-09-30T08:00:00Z" }]),
    actOnAlert: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

const manager = new Set(["reports.view", "observability.manage"]);

describe("system section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the system tab for a session holding reports.view", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).toContain("Sistem sağlığı");
  });

  it("shows alerts and unhealthy checks in Turkish, never the raw enum", async () => {
    await render(<SystemSection capabilities={manager} client={fakeClient()} />);
    expect(document.body.textContent).toContain("Kritik · Açık");
    expect(document.body.textContent).toContain("Kısmen bozuk");
    for (const raw of ["Critical", "Degraded"]) expect(document.body.textContent).not.toContain(raw);
  });

  it("without observability.manage the alert action buttons are absent", async () => {
    await render(<SystemSection capabilities={new Set(["reports.view"])} client={fakeClient()} />);
    for (const action of ["Gördüm", "Üst seviyeye taşı", "Sustur", "Çözüldü"]) expect(buttons()).not.toContain(action);
  });

  it("acknowledges an alert with its current row version and reloads", async () => {
    const client = fakeClient();
    await render(<SystemSection capabilities={manager} client={client} />);
    await press("Gördüm");
    await submit("Yazıcı yanıt vermiyor");
    expect(client.actOnAlert).toHaveBeenCalledWith(expect.objectContaining({ alertId: "a1", rowVersion: 7 }), "acknowledge", "");
    expect(client.activeAlerts).toHaveBeenCalledTimes(2);
  });

  it("resolving needs a reason", async () => {
    const client = fakeClient();
    await render(<SystemSection capabilities={manager} client={client} />);
    await press("Çözüldü");
    await submit("Yazıcı yanıt vermiyor");
    expect(alertText()).toBe("Çözüm gerekçesini yazın.");
    expect(client.actOnAlert).not.toHaveBeenCalled();
    await type("al-reason", "Kablo takıldı");
    await submit("Yazıcı yanıt vermiyor");
    expect(client.actOnAlert).toHaveBeenCalledWith(expect.anything(), "resolve", "Kablo takıldı");
  });

  it("shows the Turkish reason from the server when the alert changed meanwhile", async () => {
    const client = fakeClient({ actOnAlert: vi.fn().mockRejectedValue(new ManagementApiError(409, "X", "Uyarı başka biri tarafından değiştirildi.")) });
    await render(<SystemSection capabilities={manager} client={client} />);
    await press("Sustur");
    await submit("Yazıcı yanıt vermiyor");
    expect(alertText()).toBe("Uyarı başka biri tarafından değiştirildi.");
  });

  it("says so when everything is healthy", async () => {
    await render(<SystemSection capabilities={manager} client={fakeClient({ activeAlerts: vi.fn().mockResolvedValue([]), unhealthyChecks: vi.fn().mockResolvedValue([]) })} />);
    expect(document.body.textContent).toContain("Etkin uyarı yok.");
    expect(document.body.textContent).toContain("Tüm kontroller sağlıklı.");
  });
});
