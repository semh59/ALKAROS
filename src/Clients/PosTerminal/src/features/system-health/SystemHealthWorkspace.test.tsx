// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
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
});
