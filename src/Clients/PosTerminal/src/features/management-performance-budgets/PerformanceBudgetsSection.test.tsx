// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, submit, type, unmount } from "../management/testKit";
import type { PerformanceBudgetsClient } from "./api";
import { PerformanceBudgetsSection } from "./index";

const listed = { defaultRequestP95Ms: 800, items: [{ metric: "request", name: "GET /api/x", p95Ms: 450 }] };

function fakeClient(overrides: Partial<PerformanceBudgetsClient> = {}): PerformanceBudgetsClient {
  return {
    list: vi.fn().mockResolvedValue(listed),
    save: vi.fn().mockResolvedValue(undefined),
    remove: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

const manager = new Set(["reports.view", "observability.manage"]);
const viewer = new Set(["reports.view"]);

afterEach(unmount);

describe("PerformanceBudgetsSection", () => {
  it("lists the budgets in Turkish with the default limit", async () => {
    await render(<PerformanceBudgetsSection capabilities={manager} client={fakeClient()} />);

    expect(document.body.textContent).toContain("GET /api/x");
    expect(document.body.textContent).toContain("İstek");
    expect(document.body.textContent).toContain("varsayılan sınır: 800 ms");
    expect(document.body.textContent).not.toContain("request");
  });

  it("offers no edit controls to a user without the manage permission", async () => {
    await render(<PerformanceBudgetsSection capabilities={viewer} client={fakeClient()} />);

    expect(buttons()).toEqual([]);
    expect(document.querySelector("form")).toBeNull();
  });

  it("saves a new budget with a decimal comma and clears the form", async () => {
    const client = fakeClient();
    await render(<PerformanceBudgetsSection capabilities={manager} client={client} />);

    await type("pb-metric", "db_command");
    await type("pb-name", " SELECT 1 ");
    await type("pb-limit", "12,5");
    await submit("Sınır ekle veya güncelle");

    expect(client.save).toHaveBeenCalledWith({ metric: "db_command", name: "SELECT 1", p95Ms: 12.5 });
    expect(document.body.textContent).toContain("Sınır kaydedildi.");
    expect((document.getElementById("pb-name") as HTMLInputElement).value).toBe("");
    expect(client.list).toHaveBeenCalledTimes(2);
  });

  it.each([["", "10"], ["   ", "10"], ["GET /x", "0"], ["GET /x", "-3"], ["GET /x", "abc"], ["GET /x", "3600001"]])(
    "rejects name %j with limit %j without calling the server", async (name, limit) => {
      const client = fakeClient();
      await render(<PerformanceBudgetsSection capabilities={manager} client={client} />);

      await type("pb-name", name);
      await type("pb-limit", limit);
      await submit("Sınır ekle veya güncelle");

      expect(client.save).not.toHaveBeenCalled();
      expect(alertText()).toContain("Ad boş olamaz");
    });

  it("edit fills the form and remove deletes the budget", async () => {
    const client = fakeClient();
    await render(<PerformanceBudgetsSection capabilities={manager} client={client} />);

    await press("Düzenle");
    expect((document.getElementById("pb-name") as HTMLInputElement).value).toBe("GET /api/x");
    expect((document.getElementById("pb-limit") as HTMLInputElement).value).toBe("450");
    await press("Sil");

    expect(client.remove).toHaveBeenCalledWith("request", "GET /api/x");
    expect(document.body.textContent).toContain("Sınır silindi.");
  });

  it("shows the server's Turkish reason when saving fails", async () => {
    const save = vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Bu işlem için yetkiniz yok."));
    await render(<PerformanceBudgetsSection capabilities={manager} client={fakeClient({ save })} />);

    await type("pb-name", "GET /x");
    await type("pb-limit", "10");
    await submit("Sınır ekle veya güncelle");

    expect(alertText()).toContain("Bu işlem için yetkiniz yok.");
  });

  it("states that no custom limit exists", async () => {
    const list = vi.fn().mockResolvedValue({ defaultRequestP95Ms: 800, items: [] });
    await render(<PerformanceBudgetsSection capabilities={manager} client={fakeClient({ list })} />);

    expect(document.body.textContent).toContain("Özel sınır tanımlı değil.");
  });
});
