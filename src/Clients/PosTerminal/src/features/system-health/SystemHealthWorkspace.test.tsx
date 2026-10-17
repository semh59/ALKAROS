// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SystemHealthWorkspace } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function baseProps(overrides: Partial<ComponentProps<typeof SystemHealthWorkspace>> = {}) {
  return {
    state: "ready" as const,
    health: {
      snapshotId: "snap-1", databaseStatus: "Healthy" as const, diskStatus: "Degraded" as const, lastBackupStatus: "Unhealthy" as const,
      freeDiskBytes: 21_474_836_480, databaseSizeBytes: 1_073_741_824, capturedAt: "2026-08-31T09:00:00Z",
    },
    backups: [{ backupId: "b-1", backupType: "Full", fileSizeBytes: 0, status: "Failed" as const, errorMessage: "engine down", startedAt: "2026-08-31T08:00:00Z", completedAt: "2026-08-31T08:01:00Z", retentionDays: 30 }],
    onRefresh: vi.fn(),
    ...overrides,
  };
}

describe("system health workspace", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("renders Turkish health statuses and does not leak raw enum values", async () => {
    await render(<SystemHealthWorkspace {...baseProps()} />);
    expect(document.body.textContent).toContain("Sistem sağlığı");
    expect(document.body.textContent).toContain("Sağlıklı");
    expect(document.body.textContent).toContain("Sınırlı");
    expect(document.body.textContent).toContain("Sorunlu");
    expect(document.body.textContent).not.toContain("Healthy");
    expect(document.body.textContent).not.toContain("engine down");
    expect(document.body.textContent).toContain("Başarısız");
  });

  it("blocks the workspace when the viewer is not a manager", async () => {
    await render(<SystemHealthWorkspace {...baseProps({ state: "unauthorized" })} />);
    expect(document.body.textContent).toContain("Yönetici oturumu gerekli");
  });

  it("marks an old snapshot as stale and shows when it was taken", async () => {
    await render(<SystemHealthWorkspace {...baseProps()} />);
    expect(document.body.textContent).toContain("Sağlık ölçümü eski");
    expect(document.body.textContent).toContain("Gösterilen değerler güncel olmayabilir.");
  });

  it("shows no stale marker for a fresh snapshot", async () => {
    const health = { snapshotId: "snap-2", databaseStatus: "Healthy" as const, diskStatus: "Healthy" as const, lastBackupStatus: "Healthy" as const, freeDiskBytes: 1, databaseSizeBytes: 1, capturedAt: new Date().toISOString() };
    await render(<SystemHealthWorkspace {...baseProps({ health })} />);
    expect(document.body.textContent).not.toContain("Sağlık ölçümü eski");
  });

  // V1-RMD-372 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces, this file had no axe-core scan at all.
  it("has no critical or serious axe violations", async () => {
    document.title = "ALKAROS sistem sağlığı";
    await render(<SystemHealthWorkspace {...baseProps()} />);
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
