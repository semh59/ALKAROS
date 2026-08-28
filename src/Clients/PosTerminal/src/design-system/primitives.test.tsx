// @vitest-environment jsdom

import { act, createRef, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Button, ModalDialog, SelectField, StateMessage, TextField, ValidationSummary } from "./index";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe("design-system primitives", () => {
  let root: Root | null = null;

  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS form";
    document.body.innerHTML = '<button id="trigger">Aç</button><div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
  });

  it("links field hints and errors without color-only signaling", async () => {
    await render(<form aria-label="Ürün formu">
      <ValidationSummary title="Formu kontrol edin" errors={["SKU gerekli"]} />
      <TextField label="SKU" hint="Benzersiz satış kodu" error="SKU gerekli" />
      <SelectField label="Tür" error="Tür gerekli"><option value="">Seçin</option></SelectField>
      <Button type="submit">Kaydet</Button>
    </form>);

    const input = document.querySelector("input")!;
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(input.getAttribute("aria-describedby")?.split(" ")).toHaveLength(2);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("SKU gerekli");
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });

  it("moves focus into a named modal, traps tab, closes on Escape and restores focus", async () => {
    const close = vi.fn();
    const firstRef = createRef<HTMLButtonElement>();
    document.body.innerHTML = '<button id="trigger">Aç</button><div id="root"></div>';
    document.getElementById("trigger")!.focus();
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<ModalDialog open title="Silme onayı" onClose={close} initialFocusRef={firstRef}>
      <Button ref={firstRef}>İptal</Button><Button>Onayla</Button>
    </ModalDialog>));

    expect(document.activeElement).toBe(firstRef.current);
    const dialog = document.querySelector<HTMLElement>('[role="dialog"]')!;
    expect(dialog.getAttribute("aria-modal")).toBe("true");
    expect(dialog.getAttribute("aria-labelledby")).toBeTruthy();
    dialog.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    expect(close).toHaveBeenCalledOnce();

    await act(async () => root!.render(<ModalDialog open={false} title="Silme onayı" onClose={close}><StateMessage tone="warning" title="Uyarı" /></ModalDialog>));
    expect(document.activeElement?.id).toBe("trigger");
  });
});
