// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { focusOnMount } from "../focusOnMount";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
});

const form = () => createElement("form", null, createElement("input", { id: "user", ref: focusOnMount }), createElement("input", { id: "pass" }));

describe("focusOnMount", () => {
  it("focuses the control when it appears and leaves focus alone on later renders", async () => {
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(form()));
    expect(document.activeElement?.id).toBe("user");

    (document.getElementById("pass") as HTMLInputElement).focus();
    await act(async () => root!.render(form()));

    expect(document.activeElement?.id).toBe("pass");
  });
});
