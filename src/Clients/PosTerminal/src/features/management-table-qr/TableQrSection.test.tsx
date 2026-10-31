// @vitest-environment jsdom

import axe from "axe-core";
import { generate } from "lean-qr";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ManagementArea } from "../management";
import { ManagementApiError } from "../management/http";
import { alertText, buttons, press, render, unmount } from "../management/testKit";
import { createTableQrClient, type TableQrClient } from "./api";
import type { TableQrRow } from "./models";
import { TableQrSection } from "./index";

const RAW_TOKEN = "alkaros-table-token:abc_DEF-123";
const QR_URL = `https://sube1.example.app/qr/?t=${encodeURIComponent(RAW_TOKEN)}`;

const row = (over: Partial<TableQrRow>): TableQrRow => ({
  tableId: "t-1", tableNumber: "1", zoneName: "Salon", tableActive: true, qrIssuedAt: null, ...over,
});

const client = (over: Partial<TableQrClient> = {}): TableQrClient => ({
  list: vi.fn().mockResolvedValue([row({}), row({ tableId: "t-2", tableNumber: "2", zoneName: null, qrIssuedAt: "2026-10-31T10:00:00Z" })]),
  issue: vi.fn().mockResolvedValue({ tableId: "t-1", qrUrl: QR_URL }),
  rotate: vi.fn().mockResolvedValue({ tableId: "t-2", qrUrl: QR_URL }),
  revoke: vi.fn().mockResolvedValue(undefined),
  ...over,
});

const byLabel = (label: string) => document.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;
const click = async (element: HTMLElement) => { await act(async () => element.click()); };
const svg = () => document.querySelector<SVGSVGElement>("svg.mtq__svg");

describe("table QR management section", () => {
  afterEach(async () => { await unmount(); vi.restoreAllMocks(); });

  it("the shell lists the Masa QR tab only for a session holding integrations.manage", async () => {
    await render(<ManagementArea capabilities={new Set(["integrations.manage"])} />);
    expect(buttons()).toContain("Masa QR");
    await render(<ManagementArea capabilities={new Set(["reports.view", "orders.create"])} />);
    expect(buttons()).not.toContain("Masa QR");
  });

  it("lists every table with its zone and whether it has a QR, and only offers the fitting action", async () => {
    await render(<TableQrSection client={client()} />);
    const rows = [...document.querySelectorAll("tbody tr")].map((tr) => tr.textContent!);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("1 · Salon");
    expect(rows[0]).toContain("QR yok");
    expect(rows[1]).toContain("2 · Bölgesiz");
    expect(rows[1]).toContain("QR var");
    expect(byLabel("QR üret 1 · Salon")).not.toBeNull();
    expect(byLabel("Yenile 1 · Salon")).toBeNull();
    expect(byLabel("Yenile 2 · Bölgesiz")).not.toBeNull();
    expect(byLabel("QR üret 2 · Bölgesiz")).toBeNull();
  });

  it("shows the QR once after issuing, with a plain warning, never prints the address as text and forgets it on close", async () => {
    const api = client();
    await render(<TableQrSection client={api} />);
    await click(byLabel("QR üret 1 · Salon"));

    expect(api.issue).toHaveBeenCalledWith("t-1");
    expect(svg()).not.toBeNull();
    expect(document.body.textContent).toContain("yalnızca şimdi görünür");
    expect(document.body.textContent).toContain("Yenile");
    expect(document.body.innerHTML).not.toContain("alkaros-table-token");
    expect(document.body.innerHTML).not.toContain("sube1.example.app");

    await press("Kapat");
    expect(svg()).toBeNull();
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it("draws the three finder patterns and every dark module of the generated code", async () => {
    await render(<TableQrSection client={client()} />);
    await click(byLabel("QR üret 1 · Salon"));

    const code = generate(QR_URL);
    const quiet = 4;
    const d = svg()!.querySelector("path")!.getAttribute("d")!;
    const dark = new Set([...d.matchAll(/M(\d+) (\d+)h1v1h-1z/g)].map((m) => `${m[1]},${m[2]}`));
    const has = (x: number, y: number) => dark.has(`${x + quiet},${y + quiet}`);

    let expected = 0;
    for (let y = 0; y < code.size; y += 1) for (let x = 0; x < code.size; x += 1) if (code.get(x, y)) expected += 1;
    expect(dark.size).toBe(expected);
    expect(svg()!.getAttribute("viewBox")).toBe(`0 0 ${code.size + quiet * 2} ${code.size + quiet * 2}`);

    // ISO 18004 finder pattern: dark 7x7 ring, light ring, dark 3x3 centre - at three corners, not the fourth.
    for (const [ox, oy] of [[0, 0], [code.size - 7, 0], [0, code.size - 7]] as const) {
      for (let y = 0; y < 7; y += 1) {
        for (let x = 0; x < 7; x += 1) {
          const ring = x === 0 || x === 6 || y === 0 || y === 6;
          const centre = x >= 2 && x <= 4 && y >= 2 && y <= 4;
          expect(has(ox + x, oy + y)).toBe(ring || centre);
        }
      }
    }
  });

  it("orients the code correctly: fixed dark module, timing patterns, and no mirror symmetry", async () => {
    await render(<TableQrSection client={client()} />);
    await click(byLabel("QR üret 1 · Salon"));

    const quiet = 4;
    const size = Number(svg()!.getAttribute("viewBox")!.split(" ")[2]) - quiet * 2;
    const d = svg()!.querySelector("path")!.getAttribute("d")!;
    const dark = new Set([...d.matchAll(/M(\d+) (\d+)h1v1h-1z/g)].map((m) => `${m[1]},${m[2]}`));
    const has = (x: number, y: number) => dark.has(`${x + quiet},${y + quiet}`);

    // ISO 18004: the module left of the bottom-left finder's right edge, one row above its ring, is always dark.
    expect(has(8, size - 8)).toBe(true);
    // Timing patterns alternate dark/light along row 6 and column 6 between the finders.
    for (let i = 8; i < size - 8; i += 1) {
      expect(has(i, 6)).toBe(i % 2 === 0);
      expect(has(6, i)).toBe(i % 2 === 0);
    }
    // Data and format modules make a real code unequal to its own transpose; a swapped x/y would not be.
    let asymmetric = false;
    for (let y = 0; y < size; y += 1) for (let x = 0; x < y; x += 1) if (has(x, y) !== has(y, x)) asymmetric = true;
    expect(asymmetric).toBe(true);
  });

  it("shows the empty state, and a failed load offers a retry that loads again", async () => {
    const list = vi.fn().mockResolvedValueOnce([]).mockRejectedValueOnce(new ManagementApiError(503, "X", "Veritabanı işlemi tamamlanamadı.")).mockResolvedValue([row({})]);
    await render(<TableQrSection client={client({ list })} />);
    expect(document.body.textContent).toContain("Henüz tanımlı masa yok");

    await unmount();
    await render(<TableQrSection client={client({ list })} />);
    expect(alertText() ?? document.body.textContent).toContain("Veritabanı işlemi tamamlanamadı.");
    await press("Tekrar dene");
    expect(byLabel("QR üret 1 · Salon")).not.toBeNull();
  });

  it("reloads the list after a revoke and after a rotate so the row shows the real state", async () => {
    const list = vi.fn()
      .mockResolvedValueOnce([row({ tableId: "t-2", tableNumber: "2", zoneName: null, qrIssuedAt: "2026-10-31T10:00:00Z" })])
      .mockResolvedValue([row({ tableId: "t-2", tableNumber: "2", zoneName: null, qrIssuedAt: null })]);
    const api = client({ list });
    await render(<TableQrSection client={api} />);
    expect(byLabel("İptal et 2 · Bölgesiz")).not.toBeNull();

    await click(byLabel("İptal et 2 · Bölgesiz"));
    await press("Vazgeç");
    expect(api.revoke).not.toHaveBeenCalled();
    await click(byLabel("İptal et 2 · Bölgesiz"));
    await press("Evet, iptal et");

    expect(list).toHaveBeenCalledTimes(2);
    expect(byLabel("QR üret 2 · Bölgesiz")).not.toBeNull();
    expect(byLabel("İptal et 2 · Bölgesiz")).toBeNull();
  });

  it("keeps the address out of the page and of browser storage after the dialog closes, including by Escape", async () => {
    const setItem = vi.spyOn(Storage.prototype, "setItem");
    await render(<TableQrSection client={client()} />);
    await click(byLabel("Yenile 2 · Bölgesiz"));
    await press("Evet, yenile");
    expect(svg()).not.toBeNull();

    await act(async () => { document.querySelector('[role="dialog"]')!.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })); });

    expect(svg()).toBeNull();
    expect(document.body.innerHTML).not.toContain("alkaros-table-token");
    expect(document.body.innerHTML).not.toContain("sube1.example.app");
    expect(setItem).not.toHaveBeenCalled();
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain("alkaros-table-token");
  });

  it("asks before rotating and before revoking, and does nothing when the manager backs out", async () => {
    const api = client();
    await render(<TableQrSection client={api} />);

    await click(byLabel("Yenile 2 · Bölgesiz"));
    expect(document.body.textContent).toContain("hemen çalışmaz hale gelecek");
    expect(api.rotate).not.toHaveBeenCalled();
    await press("Vazgeç");
    expect(api.rotate).not.toHaveBeenCalled();

    await click(byLabel("Yenile 2 · Bölgesiz"));
    await press("Evet, yenile");
    expect(api.rotate).toHaveBeenCalledWith("t-2");
    expect(svg()).not.toBeNull();

    await press("Kapat");
    await click(byLabel("İptal et 2 · Bölgesiz"));
    expect(api.revoke).not.toHaveBeenCalled();
    await press("Evet, iptal et");
    expect(api.revoke).toHaveBeenCalledWith("t-2");
    expect(document.body.textContent).toContain("QR iptal edildi");
    expect(svg()).toBeNull();
  });

  it("shows the server's Turkish reason when the relay is not ready and opens no code", async () => {
    const api = client({
      issue: vi.fn().mockRejectedValue(new ManagementApiError(409, "RELAY_NOT_READY", "Uzaktan sipariş bağlantısı henüz etkinleştirilmedi; önce bağlantı ayarlarından etkinleştirin.")),
    });
    await render(<TableQrSection client={api} />);
    await click(byLabel("QR üret 1 · Salon"));

    expect(alertText()).toContain("etkinleştirilmedi");
    expect(svg()).toBeNull();
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it("falls back to a Turkish message for an unexpected failure and never prints the raw error", async () => {
    const api = client({ revoke: vi.fn().mockRejectedValue(new Error("connection reset")) });
    await render(<TableQrSection client={api} />);
    await click(byLabel("İptal et 2 · Bölgesiz"));
    await press("Evet, iptal et");

    expect(alertText()).toBe("İşlem tamamlanamadı.");
  });

  it("prints through the browser", async () => {
    const print = vi.fn();
    vi.stubGlobal("print", print);
    await render(<TableQrSection client={client()} />);
    await click(byLabel("QR üret 1 · Salon"));
    await press("Yazdır");

    expect(print).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it("has no accessibility violations with the code dialog open", async () => {
    await render(<TableQrSection client={client()} />);
    await click(byLabel("QR üret 1 · Salon"));

    const result = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(result.violations).toEqual([]);
  });

  it("print styles hide everything except the label", () => {
    const css = readFileSync(join(__dirname, "management-table-qr.css"), "utf8");
    const printRules = css.slice(css.indexOf("@media print"));
    expect(printRules).toMatch(/body \*\s*\{\s*visibility:\s*hidden/);
    expect(printRules).toMatch(/\.mtq__label[^{]*\{\s*visibility:\s*visible/);
  });
});

describe("table QR client", () => {
  const respond = (body: unknown, status = 200) =>
    vi.fn().mockImplementation(async () => new Response(status === 204 ? null : JSON.stringify(body), { status }));

  it("calls the manager-only routes with the right methods", async () => {
    const fetcher = respond([]);
    const api = createTableQrClient(fetcher);
    await api.list();
    expect(fetcher.mock.calls[0]![0]).toBe("/api/v1/management/qr-tables");

    const post = respond({ tableId: "t-1", qrUrl: QR_URL });
    const posting = createTableQrClient(post);
    await posting.issue("t-1");
    await posting.rotate("t-1");
    expect(post.mock.calls.map((call) => [call[0], call[1].method])).toEqual([
      ["/api/v1/management/qr-tables/t-1/issue", "POST"],
      ["/api/v1/management/qr-tables/t-1/rotate", "POST"],
    ]);

    const revoke = respond(null, 204);
    await createTableQrClient(revoke).revoke("t-1");
    expect(revoke.mock.calls[0]![0]).toBe("/api/v1/management/qr-tables/t-1/revoke");
  });
});
