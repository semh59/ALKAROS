// @vitest-environment jsdom

import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { StaffClient } from "./api";
import { PoliciesPanel } from "./PoliciesPanel";
import { ManagementApiError } from "../management/http";
import { buttons, press, render, submit, type, unmount } from "../management/testKit";

const discount = { permissionCode: "bills.discount", roleCode: "waiter", limitAmount: 100, maxCount: 10, windowSeconds: 28800, rowVersion: 3 };

function setUp(overrides: Partial<StaffClient> = {}) {
  const api = {
    listPolicies: vi.fn().mockResolvedValue([discount]),
    setPolicy: vi.fn().mockResolvedValue(undefined),
    removePolicy: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as unknown as StaffClient;
  const setNotice = vi.fn();
  const onChanged = vi.fn();
  return { api, setNotice, onChanged };
}

const rowOf = (role: string, action: string) =>
  [...document.querySelectorAll("tbody tr")].find((row) => row.textContent?.includes(role) && row.textContent?.includes(action))!;
const edit = (role: string, action: string) => act(async () => (rowOf(role, action).querySelector("button") as HTMLButtonElement).click());
const fill = async (limit: string, count: string, hours: string) => {
  await type("p-limit", limit);
  await type("p-count", count);
  await type("p-hours", hours);
};

describe("authorization limits panel", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  async function open(overrides: Partial<StaffClient> = {}) {
    const fakes = setUp(overrides);
    await render(<PoliciesPanel api={fakes.api} revision={0} onChanged={fakes.onChanged} setNotice={fakes.setNotice} />);
    return fakes;
  }

  it("shows the limit that exists and says a manager decides everywhere else", async () => {
    await open();

    expect(rowOf("Garson", "İndirim").textContent).toContain("Otomatik: 100 TL'ye kadar, 8 saatte en çok 10 işlem");
    expect(rowOf("Garson", "İkram").textContent).toContain("Yönetici onaylar");
    expect(rowOf("Kasiyer", "Gönderilmiş kalem iptali").textContent).toContain("Yönetici onaylar");
    expect(document.body.textContent).not.toContain("bills.");
  });

  it("asks before saving a new limit and sends it in seconds without a version", async () => {
    const { api, setNotice, onChanged } = await open();
    await edit("Garson", "İkram");
    await fill("100", "5", "2");
    await submit("Limiti düzenle");

    expect(document.body.textContent).toContain("Bu limit kaydedilsin mi? 100 TL'ye kadar, 2 saatte en çok 5 işlem yönetici onayı beklemez.");
    expect(api.setPolicy).not.toHaveBeenCalled();

    await press("Evet");
    expect(api.setPolicy).toHaveBeenCalledWith(
      { permissionCode: "bills.comp", roleCode: "waiter", limitAmount: 100, maxCount: 5, windowSeconds: 7200 }, null);
    expect(setNotice).toHaveBeenLastCalledWith({ tone: "success", text: "Limit kaydedildi." });
    expect(onChanged).toHaveBeenCalled();
  });

  it("changes an existing limit under the version it was read with", async () => {
    const { api } = await open();
    await edit("Garson", "İndirim");
    await fill("150", "10", "8");
    await submit("Limiti düzenle");
    await press("Evet");

    expect(api.setPolicy).toHaveBeenCalledWith(expect.objectContaining({ permissionCode: "bills.discount", limitAmount: 150 }), 3);
  });

  it("does not save when the manager backs out at the question", async () => {
    const { api } = await open();
    await edit("Garson", "İkram");
    await fill("100", "5", "2");
    await submit("Limiti düzenle");
    await press("Vazgeç");

    expect(api.setPolicy).not.toHaveBeenCalled();
  });

  it("refuses values outside the allowed range without asking or calling the server", async () => {
    const { api, setNotice } = await open();
    await edit("Garson", "İkram");

    for (const [limit, count, hours] of [["1001", "5", "2"], ["0", "5", "2"], ["100", "0", "2"], ["100", "101", "2"], ["100", "5", "0"], ["100", "5", "25"], ["100", "5", "1.5"]]) {
      await fill(limit, count, hours);
      await submit("Limiti düzenle");
      expect(setNotice).toHaveBeenLastCalledWith({ tone: "error", text: "Tutar 1-1000 TL, işlem sayısı 1-100 ve süre 1-24 saat olmalı." });
    }
    expect(document.body.textContent).not.toContain("Bu limit kaydedilsin mi?");
    expect(api.setPolicy).not.toHaveBeenCalled();
  });

  it("removes a limit only after a question and only offers it where a limit exists", async () => {
    const { api, setNotice } = await open();
    await edit("Garson", "İkram");
    expect(buttons()).not.toContain("Yönetici onaylasın");
    await press("Vazgeç");

    await edit("Garson", "İndirim");
    await press("Yönetici onaylasın");
    expect(document.body.textContent).toContain("Limit kaldırılsın mı?");
    expect(api.removePolicy).not.toHaveBeenCalled();

    await press("Evet");
    expect(api.removePolicy).toHaveBeenCalledWith("bills.discount", "waiter");
    expect(setNotice).toHaveBeenLastCalledWith({ tone: "success", text: "Limit kaldırıldı; istekler yöneticiye gider." });
  });

  it("shows the server's Turkish reason when saving is refused", async () => {
    const reason = "Bu limit siz bakarken başka biri tarafından değiştirildi; listeyi yenileyip tekrar deneyin.";
    const { setNotice } = await open({ setPolicy: vi.fn().mockRejectedValue(new ManagementApiError(409, "POLICY_CHANGED", reason)) });
    await edit("Garson", "İndirim");
    await fill("120", "10", "8");
    await submit("Limiti düzenle");
    await press("Evet");

    expect(setNotice).toHaveBeenLastCalledWith({ tone: "error", text: reason });
  });
});
