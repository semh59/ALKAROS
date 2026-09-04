// @vitest-environment jsdom

import { act, type ComponentProps, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthorizationDecisionsWorkspace } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function baseProps(
  overrides: Partial<ComponentProps<typeof AuthorizationDecisionsWorkspace>> = {},
) {
  return {
    state: "ready" as const,
    pendingGrants: [
      {
        grantId: "11111111-1111-1111-1111-111111111111",
        permissionCode: "bills.comp",
        requesterUserId: "aaaaaaaa-0000-0000-0000-000000000000",
        requesterRoleCode: "waiter",
        amount: 120,
        reasonCode: "CustomerChange",
        requestedAt: "2026-09-04T18:00:00Z",
      },
    ],
    delegations: [
      {
        delegationId: "22222222-2222-2222-2222-222222222222",
        permissionCode: "bills.discount",
        granteeUserId: "bbbbbbbb-0000-0000-0000-000000000000",
        limitAmount: 200,
        expiresAt: "2026-09-04T22:00:00Z",
      },
    ],
    tightenings: [
      {
        tighteningId: "33333333-3333-3333-3333-333333333333",
        userId: "cccccccc-0000-0000-0000-000000000000",
        permissionCode: "bills.void",
        recentCount: 6,
        triggerRatio: 4,
        triggeredAt: "2026-09-04T17:00:00Z",
      },
    ],
    onApprove: vi.fn(),
    onDeny: vi.fn(),
    onRevokeDelegation: vi.fn(),
    onClearTightening: vi.fn(),
    onRefresh: vi.fn(),
    ...overrides,
  };
}

describe("authorization decisions workspace", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
  });

  it("renders the three groups with Turkish permission and reason labels, not raw codes", async () => {
    await render(<AuthorizationDecisionsWorkspace {...baseProps()} />);

    expect(document.body.textContent).toContain("Yetki kararları");
    expect(document.body.textContent).toContain("Bekleyen yetki istekleri");
    expect(document.body.textContent).toContain("İkram");
    expect(document.body.textContent).toContain("Müşteri talebi");
    expect(document.body.textContent).toContain("Etkin süreli devirler");
    expect(document.body.textContent).toContain("Açık davranışsal sıkılaştırmalar");
    expect(document.body.textContent).not.toContain("bills.comp");
    expect(document.body.textContent).not.toContain("CustomerChange");
  });

  it("wires approve, deny, revoke and clear to their callbacks", async () => {
    const props = baseProps();
    await render(<AuthorizationDecisionsWorkspace {...props} />);

    const buttons = [...document.querySelectorAll("button")];
    const click = (label: string) => {
      const button = buttons.find((b) => b.textContent?.trim() === label);
      if (!button) throw new Error(`button not found: ${label}`);
      button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    };

    await act(async () => click("Onayla"));
    await act(async () => click("Reddet"));
    await act(async () => click("Devri geri al"));
    await act(async () => click("Sıkılaştırmayı kaldır"));

    expect(props.onApprove).toHaveBeenCalledWith("11111111-1111-1111-1111-111111111111");
    expect(props.onDeny).toHaveBeenCalledWith("11111111-1111-1111-1111-111111111111");
    expect(props.onRevokeDelegation).toHaveBeenCalledWith("22222222-2222-2222-2222-222222222222");
    expect(props.onClearTightening).toHaveBeenCalledWith("33333333-3333-3333-3333-333333333333");
  });

  it("shows the empty copy when nothing is pending", async () => {
    await render(
      <AuthorizationDecisionsWorkspace
        {...baseProps({ pendingGrants: [], delegations: [], tightenings: [] })}
      />,
    );

    expect(document.body.textContent).toContain("Bekleyen yetki isteği yok.");
    expect(document.body.textContent).toContain("Etkin devir yok.");
    expect(document.body.textContent).toContain("Açık sıkılaştırma yok.");
  });

  it("blocks the workspace for a non-manager viewer", async () => {
    await render(<AuthorizationDecisionsWorkspace {...baseProps({ state: "unauthorized" })} />);

    expect(document.body.textContent).toContain("Yönetici oturumu gerekli");
    expect(document.body.textContent).toContain("şef garson");
  });
});
