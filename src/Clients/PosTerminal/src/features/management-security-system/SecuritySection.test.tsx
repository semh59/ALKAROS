// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { SecurityApiError, type SecurityClient } from "./api";
import { SecuritySection } from "./index";
import { alertText, buttons, press, render, submit, type, unmount } from "./testKit";

const job = { name: "retention-sweep", description: "Saklama süresi dolan kayıtları imha eder.", intervalSeconds: 86_400, enabled: true, disabledReason: null, lastStatus: "NeverRun", lastRunAt: null, lastDurationMilliseconds: null, lastSummary: null };

function fakeClient(overrides: Partial<SecurityClient> = {}): SecurityClient {
  return {
    lookupUser: vi.fn().mockResolvedValue({ userId: "u1", displayName: "Ali Veli", active: true, isLocked: true }),
    revokeSessions: vi.fn().mockResolvedValue(2),
    forceUnlock: vi.fn().mockResolvedValue(undefined),
    listJobs: vi.fn().mockResolvedValue([job, { ...job, name: "offsite-backup", description: "Yedeği uzak hedefe yükler.", enabled: false, disabledReason: "Uzak hedef tanımlı değil." }]),
    runJob: vi.fn().mockResolvedValue({ ...job, lastStatus: "Succeeded" }),
    backupRpo: vi.fn().mockResolvedValue([
      { dataClass: "Fiscal", targetSeconds: 3600, measuredGapSeconds: null, meetsTarget: false },
      { dataClass: "Settings", targetSeconds: 86_400, measuredGapSeconds: 7200, meetsTarget: true },
    ]),
    restoreAttempts: vi.fn().mockResolvedValue([]),
    diagnosticBundle: vi.fn().mockResolvedValue({ bundleId: "b1", generatedAt: "2026-09-30T10:00:00Z", sizeBytes: 4096, logEntries: [{}, {}, {}], systemStatus: { unhealthyCheckCount: 1 } }),
    orderBacklog: vi.fn().mockResolvedValue({ liveOrders: 12, provablySettled: 5, withoutBill: 3, withOpenBill: 4, oldestSettledCreatedAt: null }),
    closeSettled: vi.fn().mockImplementation(async (dryRun: boolean) => ({ dryRun, eligible: 5, closed: dryRun ? 0 : 5, failed: 0, sampleOrderNumbers: ["S-1", "S-2"] })),
    ...overrides,
  };
}

const caps = new Set(["security.manage"]);

describe("security section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the security tab only for a session holding security.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["reports.view"])} />);
    expect(buttons()).not.toContain("Güvenlik");
    await render(<ManagementArea capabilities={new Set(["reports.view", "security.manage"])} />);
    expect(buttons()).toContain("Güvenlik");
  });

  it("finds a locked account, ends its sessions and clears the lock", async () => {
    const client = fakeClient();
    await render(<SecuritySection capabilities={caps} client={client} />);
    await type("rec-username", "ali");
    await press("Ara");
    expect(document.body.textContent).toContain("Ali Veli");
    expect(document.body.textContent).toContain("Kilitli");
    await press("Tüm Oturumları Sonlandır");
    expect(client.revokeSessions).toHaveBeenCalledWith("u1");
    expect(document.body.textContent).toContain("Tüm oturumlar sonlandırıldı (2 oturum).");
    await press("Hesap Kilidini Kaldır");
    expect(client.forceUnlock).toHaveBeenCalledWith("u1");
    const unlock = [...document.querySelectorAll("button")].find((button) => button.textContent === "Hesap Kilidini Kaldır") as HTMLButtonElement;
    expect(unlock.disabled).toBe(true);
  });

  it("an unknown username gets a Turkish message", async () => {
    await render(<SecuritySection capabilities={caps} client={fakeClient({ lookupUser: vi.fn().mockResolvedValue(null) })} />);
    await type("rec-username", "yok");
    await press("Ara");
    expect(alertText()).toBe("Bu kullanıcı adıyla bir hesap bulunamadı.");
  });

  it("shows maintenance jobs in Turkish, runs one and keeps a disabled job from running", async () => {
    const client = fakeClient();
    await render(<SecuritySection capabilities={caps} client={client} />);
    expect(document.body.textContent).toContain("Hiç çalışmadı");
    expect(document.body.textContent).not.toContain("NeverRun");
    expect(document.body.textContent).toContain("Uzak hedef tanımlı değil.");
    const runButtons = [...document.querySelectorAll("button")].filter((button) => button.textContent === "Şimdi çalıştır") as HTMLButtonElement[];
    expect(runButtons.map((button) => button.disabled)).toEqual([false, true]);
    await press("Şimdi çalıştır");
    expect(client.runJob).toHaveBeenCalledWith("retention-sweep");
    expect(document.body.textContent).toContain("İş çalıştırıldı: Başarılı.");
  });

  it("shows the backup coverage per data class and never hides a shortfall", async () => {
    await render(<SecuritySection capabilities={caps} client={fakeClient()} />);
    expect(document.body.textContent).toContain("Mali kayıtlar");
    expect(document.body.textContent).toContain("Yedek makbuzu yok");
    expect(document.body.textContent).toContain("Hedefin dışında");
    expect(document.body.textContent).toContain("Hedefe uygun");
    expect(document.body.textContent).not.toContain("Fiscal");
    expect(document.body.textContent).toContain("Kayıtlı geri yükleme denemesi yok.");
  });

  it("closing settled orders previews first and closes only after the explicit confirmation", async () => {
    const client = fakeClient();
    await render(<SecuritySection capabilities={caps} client={client} />);
    expect(buttons().some((text) => text?.includes("siparişi kapat"))).toBe(false);
    await press("Kapatılabilecekleri göster");
    expect(client.closeSettled).toHaveBeenCalledTimes(1);
    expect(client.closeSettled).toHaveBeenLastCalledWith(true);
    expect(document.body.textContent).toContain("Kapatılabilecek 5 sipariş var (S-1, S-2)");
    await press("5 siparişi kapat");
    expect(client.closeSettled).toHaveBeenLastCalledWith(false);
    expect(document.body.textContent).toContain("Kapatılan: 5, kapatılamayan: 0.");
  });

  it("says so when there is nothing to close", async () => {
    const client = fakeClient({ closeSettled: vi.fn().mockResolvedValue({ dryRun: true, eligible: 0, closed: 0, failed: 0, sampleOrderNumbers: [] }) });
    await render(<SecuritySection capabilities={caps} client={client} />);
    await press("Kapatılabilecekleri göster");
    expect(document.body.textContent).toContain("Kapatılabilecek sipariş yok.");
  });

  it("a diagnostic bundle needs ids, a window and a reason, then sends them", async () => {
    const client = fakeClient();
    await render(<SecuritySection capabilities={caps} client={client} />);
    await submit("Tanılama paketi");
    expect(alertText()).toBe("Korelasyon kimliği, tarih aralığı ve gerekçe girin.");
    expect(client.diagnosticBundle).not.toHaveBeenCalled();
    await type("dg-ids", "abc-1, abc-2");
    await type("dg-from", "2026-09-29T08:00");
    await type("dg-to", "2026-09-30T08:00");
    await type("dg-reason", "Şikâyet incelemesi");
    await submit("Tanılama paketi");
    expect(client.diagnosticBundle).toHaveBeenCalledWith({
      correlationIds: ["abc-1", "abc-2"], windowStart: new Date("2026-09-29T08:00").toISOString(), windowEnd: new Date("2026-09-30T08:00").toISOString(), reason: "Şikâyet incelemesi",
    });
    expect(document.body.textContent).toContain("3 kayıt, 1 sağlıksız kontrol, yaklaşık 4 KB.");
    expect(buttons()).toContain("Paketi indir");
  });

  it("the Turkish reason for a refused bundle is shown as is", async () => {
    const client = fakeClient({ diagnosticBundle: vi.fn().mockRejectedValue(new SecurityApiError(400, "TIME_WINDOW_TOO_LARGE", "Zaman aralığı en fazla 30 gün olabilir.")) });
    await render(<SecuritySection capabilities={caps} client={client} />);
    await type("dg-ids", "abc-1");
    await type("dg-from", "2026-01-01T08:00");
    await type("dg-to", "2026-09-30T08:00");
    await type("dg-reason", "Deneme");
    await submit("Tanılama paketi");
    expect(alertText()).toBe("Zaman aralığı en fazla 30 gün olabilir.");
  });
});
