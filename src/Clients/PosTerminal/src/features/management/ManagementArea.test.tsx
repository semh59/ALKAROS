// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { ManagementArea } from "./index";
import type { ManagementSection } from "./sections";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const sections: readonly ManagementSection[] = [
  { id: "a", label: "Birinci bölüm", requiredCapability: "reports.view", component: () => <p>Birinci içerik</p> },
  { id: "b", label: "İkinci bölüm", requiredCapability: "inventory.manage", component: () => <p>İkinci içerik</p> },
];

describe("management area shell", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; });

  it("lists only the sections the session may use", async () => {
    await render(<ManagementArea sections={sections} capabilities={new Set(["reports.view"])} />);
    expect(document.body.textContent).toContain("Birinci bölüm");
    expect(document.body.textContent).not.toContain("İkinci bölüm");
  });

  it("switches the content when a section tab is pressed", async () => {
    await render(<ManagementArea sections={sections} capabilities={new Set(["reports.view", "inventory.manage"])} />);
    expect(document.body.textContent).toContain("Birinci içerik");
    const second = [...document.querySelectorAll("button")].find((button) => button.textContent === "İkinci bölüm")!;
    await act(async () => second.click());
    expect(document.body.textContent).toContain("İkinci içerik");
    expect(second.getAttribute("aria-current")).toBe("page");
  });

  it("a session with no permitted section sees a Turkish explanation", async () => {
    await render(<ManagementArea sections={sections} capabilities={new Set()} />);
    expect(document.body.textContent).toContain("Bu oturumda açılabilir bölüm yok");
  });
});
