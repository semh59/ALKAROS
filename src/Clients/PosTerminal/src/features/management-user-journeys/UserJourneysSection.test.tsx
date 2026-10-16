// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementApiError } from "../management/http";
import { buttons, press, render, type, unmount } from "../management/testKit";
import type { UserJourneysClient } from "./api";
import { UserJourneysSection } from "./index";

const request = {
  traceId: "a".repeat(32), method: "POST", route: "/api/v1/bills/pay", statusCode: 200, startedAt: "2026-10-16T09:00:00Z",
  durationMilliseconds: 842, spanCount: 2, failedSpanCount: 0,
};
const journey = {
  traceId: request.traceId, startedAt: request.startedAt, requestCaptured: true, droppedSpans: 0,
  steps: [
    { kind: "request", name: "POST /api/v1/bills/pay (200)", offsetMilliseconds: 0, durationMilliseconds: 842, failed: false },
    { kind: "database", name: "select from bills", offsetMilliseconds: 12, durationMilliseconds: 30, failed: false },
    { kind: "event", name: "billing.bill.paid", offsetMilliseconds: 800, durationMilliseconds: 0, failed: true },
  ],
};

function fakeClient(overrides: Partial<UserJourneysClient> = {}): UserJourneysClient {
  return {
    actions: vi.fn().mockResolvedValue([{ key: "b.pay.1", count: 3, averageMilliseconds: 250, maxMilliseconds: 600, errorCount: 1 }]),
    requests: vi.fn().mockResolvedValue([request]),
    journey: vi.fn().mockResolvedValue(journey),
    staffMovements: vi.fn().mockResolvedValue([{ occurredAt: "2026-10-16T09:00:00Z", actorId: null, actorName: "Ayşe", eventName: "staff.moved", aggregateType: "Staff" }]),
    ...overrides,
  };
}

const viewer = new Set(["reports.view"]);
const manager = new Set(["reports.view", "observability.manage"]);

describe("user journeys section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("explains the technical terms and shows a loading message first", async () => {
    await render(<UserJourneysSection capabilities={viewer} client={fakeClient({ requests: () => new Promise(() => undefined) })} />);
    expect(document.body.textContent).toContain("İstek: ekranın sunucuya yaptığı çağrı.");
    expect(document.body.textContent).toContain("Yükleniyor…");
  });

  it("opening a request shows its steps in order with durations", async () => {
    const client = fakeClient();
    await render(<UserJourneysSection capabilities={viewer} client={client} />);
    await press("Zaman çizelgesi");

    expect(client.journey).toHaveBeenCalledWith(request.traceId);
    const rows = [...document.querySelectorAll("tbody tr")].map((row) => row.textContent ?? "");
    const stepRows = rows.slice(1);
    expect(stepRows[0]).toContain("İstek");
    expect(stepRows[1]).toContain("Veritabanı");
    expect(stepRows[2]).toContain("Olay");
    expect(stepRows[2]).toContain("başarısız");
    expect(stepRows[0]).toContain("842 ms");
  });

  it("says so when the request has left the server memory", async () => {
    const client = fakeClient({ journey: vi.fn().mockResolvedValue({ ...journey, requestCaptured: false }) });
    await render(<UserJourneysSection capabilities={viewer} client={client} />);
    await press("Zaman çizelgesi");
    expect(document.body.textContent).toContain("sunucu belleğinde artık yok");
  });

  it("a request step kind it does not know is shown as Bilinmiyor", async () => {
    const client = fakeClient({ journey: vi.fn().mockResolvedValue({ ...journey, steps: [{ ...journey.steps[0], kind: "zzz" }] }) });
    await render(<UserJourneysSection capabilities={viewer} client={client} />);
    await press("Zaman çizelgesi");
    expect(document.body.textContent).toContain("Bilinmiyor");
  });

  it("the actions view passes the role and terminal filters and ignores a malformed terminal id", async () => {
    const client = fakeClient();
    await render(<UserJourneysSection capabilities={viewer} client={client} />);
    await press("Düğmeler");
    await type("uj-role", "waiter");
    expect(client.actions).toHaveBeenLastCalledWith({ sort: "slowest", role: "waiter", terminalId: "" });
    await type("uj-terminal", "xyz");
    expect(client.actions).toHaveBeenLastCalledWith({ sort: "slowest", role: "waiter", terminalId: "" });
    expect(document.body.textContent).toContain("Geçerli bir cihaz kimliği yazın.");
    const id = "11111111-2222-3333-4444-555555555555";
    await type("uj-terminal", id);
    expect(client.actions).toHaveBeenLastCalledWith({ sort: "slowest", role: "waiter", terminalId: id });
  });

  it("staff movements are offered only with observability.manage", async () => {
    await render(<UserJourneysSection capabilities={viewer} client={fakeClient()} />);
    expect(buttons()).not.toContain("Personel hareketleri");
    await unmount();
    const client = fakeClient();
    await render(<UserJourneysSection capabilities={manager} client={client} />);
    await press("Personel hareketleri");
    expect(client.staffMovements).toHaveBeenCalled();
    expect(document.body.textContent).toContain("Ayşe");
  });

  it("shows a retryable error without the raw failure text, and the Turkish unauthorized message", async () => {
    await render(<UserJourneysSection capabilities={viewer} client={fakeClient({ requests: vi.fn().mockRejectedValue(new Error("ECONNRESET")) })} />);
    expect(document.body.textContent).toContain("Liste alınamadı.");
    expect(document.body.textContent).not.toContain("ECONNRESET");
    expect(buttons()).toContain("Tekrar dene");
    await unmount();
    await render(<UserJourneysSection capabilities={viewer} client={fakeClient({ requests: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Bu işlem için yetkiniz yok.")) })} />);
    expect(document.body.textContent).toContain("Bu işlem için yetkiniz yok.");
  });

  it("shows empty messages", async () => {
    await render(<UserJourneysSection capabilities={viewer} client={fakeClient({ requests: vi.fn().mockResolvedValue([]) })} />);
    expect(document.body.textContent).toContain("Gösterilecek istek yok.");
  });
});
