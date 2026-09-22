// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ProductionShell, allowedNavigation, classifyViewport, type ProductionShellProps, type ShellIdentity } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const navigation = [
  { id: "cash", label: "Kasa", href: "/cash", icon: "sales", requiredCapability: "cash.read" },
  { id: "tables", label: "Masalar", href: "/tables", icon: "tables", requiredCapability: "tables.read" },
  { id: "catalog", label: "Menü ve katalog", href: "/catalog", icon: "catalog", requiredCapability: "catalog.manage" },
] as const;

const identity: ShellIdentity = {
  branchName: "Merkez",
  terminalName: "Kasa 2",
  userName: "Deniz Kaya",
  roleLabel: "Kasiyer",
  capabilities: new Set(["cash.read", "tables.read"]),
};

const baseProps: ProductionShellProps = {
  session: { status: "authenticated", identity },
  authorization: { status: "authorized" },
  connectivity: { status: "online" },
  freshness: { status: "fresh", dateTime: "2026-08-26T10:00:00Z", label: "10:00 itibarıyla güncel" },
  navigation,
  activeNavigationId: "cash",
  workspaceTitle: "Kasa",
  workspaceDescription: "Aktif satış çalışma alanı",
  contextTitle: "Sipariş bağlamı",
  context: <p>Bağlam içeriği</p>,
  children: <section aria-label="Satış içeriği">İçerik</section>,
};

describe("production shell", () => {
  let root: Root | null = null;

  async function render(element: ReactElement, width = 1280) {
    Object.defineProperty(window, "innerWidth", { value: width, writable: true, configurable: true });
    document.documentElement.lang = "tr";
    document.title = "ALKAROS";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
  });

  it("filters navigation by capability and preserves the active route contract", async () => {
    await render(<ProductionShell {...baseProps} />);

    const links = [...document.querySelectorAll<HTMLAnchorElement>("nav a")];
    expect(links.map((link) => link.textContent)).toEqual(["Kasa", "Masalar"]);
    expect(links[0].getAttribute("aria-current")).toBe("page");
    expect(document.body.textContent).toContain("Merkez");
    expect(document.body.textContent).toContain("Kasa 2");
    expect(document.body.textContent).toContain("Deniz Kaya");
    expect(document.querySelector('.ds-drawer--persistent')).not.toBeNull();
    expect(document.querySelector(".production-shell__brand")?.hasAttribute("aria-label")).toBe(false);
    // V1-RMD-256: the brand mark is now the real ALKAROS logo image, not
    // plain "ALKAROS" text.
    const brandMark = document.querySelector<HTMLImageElement>(".production-shell__brand-mark");
    expect(brandMark?.tagName).toBe("IMG");
    expect(brandMark?.alt).toBe("ALKAROS");
    expect(brandMark?.getAttribute("src")).toBeTruthy();
    expect(document.querySelector('.production-shell__identity[role="group"][aria-label="Aktif çalışma bağlamı"]')).not.toBeNull();
  });

  it("keeps missing and expired sessions distinct from forbidden access", async () => {
    const signIn = vi.fn();
    await render(<ProductionShell {...baseProps} session={{ status: "unauthenticated", reason: "expired", onSignIn: signIn }} />);
    expect(document.querySelector("nav a")).toBeNull();
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Oturumunuzun süresi doldu");

    await act(async () => root!.render(<ProductionShell {...baseProps} authorization={{ status: "forbidden" }} />));
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("erişim izniniz yok");
    expect(document.querySelector('[role="alert"]')?.textContent).not.toContain("süresi doldu");
  });

  it("never hides offline and stale recovery status", async () => {
    const retry = vi.fn();
    const refresh = vi.fn();
    await render(<ProductionShell {...baseProps} connectivity={{ status: "offline", onRetry: retry }} freshness={{ status: "stale", dateTime: "2026-08-26T09:45:00Z", label: "15 dakika önce", onRefresh: refresh }} />, 390);

    const status = document.querySelector('footer[aria-label="Sistem durumu"]')!;
    expect(status.textContent).toContain("Çevrimdışı");
    expect(status.textContent).toContain("15 dakika önce");
    const buttons = [...status.querySelectorAll("button")];
    await act(async () => buttons[0].click());
    await act(async () => buttons[1].click());
    expect(retry).toHaveBeenCalledOnce();
    expect(refresh).toHaveBeenCalledOnce();
  });

  it("turns context into an explicit sheet below the wide breakpoint", async () => {
    const open = vi.fn();
    const close = vi.fn();
    await render(<ProductionShell {...baseProps} onContextOpen={open} onContextClose={close} />, 1024);
    expect(document.querySelector('.ds-drawer')).toBeNull();
    const trigger = [...document.querySelectorAll("button")].find((button) => button.textContent === "Bağlamı aç")!;
    await act(async () => trigger.click());
    expect(open).toHaveBeenCalledOnce();

    await act(async () => root!.render(<ProductionShell {...baseProps} contextOpen onContextOpen={open} onContextClose={close} />));
    const sheet = document.querySelector<HTMLElement>('.ds-drawer--sheet')!;
    expect(sheet.getAttribute("role")).toBe("dialog");
    expect(sheet.getAttribute("aria-modal")).toBe("true");
    const closeButton = document.querySelector<HTMLButtonElement>('[aria-label="Bağlam panelini kapat"]')!;
    expect(document.activeElement).toBe(closeButton);
    await act(async () => sheet.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })));
    expect(close).toHaveBeenCalledOnce();
  });

  it("has no critical or serious automated accessibility violations", async () => {
    await render(<ProductionShell {...baseProps} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});

describe("shell contracts", () => {
  it.each([[320, "mobile"], [767, "mobile"], [768, "compact"], [1279, "compact"], [1280, "wide"], [1920, "wide"]] as const)("classifies %i px as %s", (width, expected) => {
    expect(classifyViewport(width)).toBe(expected);
  });

  it("does not treat hidden routes as authorization", () => {
    expect(allowedNavigation(navigation, new Set(["tables.read"])).map((item) => item.id)).toEqual(["tables"]);
  });
});
