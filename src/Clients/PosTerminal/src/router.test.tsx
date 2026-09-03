// @vitest-environment jsdom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { RouterProvider, isPlainClick, useRouter } from "./router";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let root: Root | null = null;
let seen: { path: string; navigate: (to: string) => void } | null = null;

function Probe() {
  seen = useRouter();
  return <span>{seen.path}</span>;
}

beforeEach(() => {
  window.history.replaceState(null, "", "/");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
});

afterEach(async () => {
  if (root) await act(async () => root!.unmount());
  root = null;
  seen = null;
});

describe("RouterProvider", () => {
  it("exposes the normalised current path", async () => {
    window.history.replaceState(null, "", "/tables/");
    await act(async () => root!.render(<RouterProvider><Probe /></RouterProvider>));
    expect(seen!.path).toBe("/tables");
  });

  it("navigate pushes history and re-renders without a reload", async () => {
    await act(async () => root!.render(<RouterProvider><Probe /></RouterProvider>));
    expect(seen!.path).toBe("/");
    await act(async () => seen!.navigate("/kitchen"));
    expect(window.location.pathname).toBe("/kitchen");
    expect(seen!.path).toBe("/kitchen");
  });

  it("syncs on popstate (back/forward)", async () => {
    await act(async () => root!.render(<RouterProvider><Probe /></RouterProvider>));
    await act(async () => seen!.navigate("/billing"));
    // A browser updates location, then fires popstate; mirror that contract.
    await act(async () => {
      window.history.replaceState(null, "", "/");
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(seen!.path).toBe("/");
  });

  it("navigate to the current path is a no-op", async () => {
    await act(async () => root!.render(<RouterProvider><Probe /></RouterProvider>));
    const before = window.history.length;
    await act(async () => seen!.navigate("/"));
    expect(window.history.length).toBe(before);
  });
});

describe("isPlainClick", () => {
  const base = { defaultPrevented: false, button: 0, metaKey: false, ctrlKey: false, shiftKey: false, altKey: false };

  it("is true for an unmodified left click", () => {
    expect(isPlainClick(base)).toBe(true);
  });

  it("is false for modifier / middle clicks and already-handled events", () => {
    expect(isPlainClick({ ...base, metaKey: true })).toBe(false);
    expect(isPlainClick({ ...base, ctrlKey: true })).toBe(false);
    expect(isPlainClick({ ...base, button: 1 })).toBe(false);
    expect(isPlainClick({ ...base, defaultPrevented: true })).toBe(false);
  });
});
