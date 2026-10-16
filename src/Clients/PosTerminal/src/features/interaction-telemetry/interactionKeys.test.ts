// @vitest-environment jsdom

import { describe, expect, it } from "vitest";
import { buildNameTable, deriveKey, isCatalogKey, routeTemplate } from "./interactionKeys";

const names = buildNameTable({ system: { save: "Kaydet", resolve: "Çözüldü" }, other: { save: "Kaydet" } });

function element(html: string): Element {
  document.body.innerHTML = html;
  return document.body.firstElementChild!;
}

describe("interaction keys", () => {
  it("prefers an explicit data-telemetry-id, also from a parent control", () => {
    const button = element('<button data-telemetry-id="pay.confirm"><span>Kaydet</span></button>');

    expect(deriveKey(button, "/cashier", names)).toBe("pay.confirm");
    expect(deriveKey(button.firstElementChild!, "/cashier", names)).toBe("pay.confirm");
  });

  it("falls back to the dictionary path of the control text, first dictionary entry winning", () => {
    expect(deriveKey(element("<button>  Kaydet </button>"), "/x", names)).toBe("t:system.save");
    expect(deriveKey(element("<button>Çözüldü</button>"), "/x", names)).toBe("t:system.resolve");
    expect(deriveKey(element('<button aria-label="Çözüldü">×</button>'), "/x", names)).toBe("t:system.resolve");
  });

  it("falls back to a route-level automatic key that never contains the text or the ids of the route", () => {
    const key = deriveKey(element("<button>Ali Veli masası</button>"), routeTemplate("/cashier/orders/42?x=1"), names);

    expect(key).toBe("auto:button@/cashier/orders/:n");
    expect(key).not.toContain("ali");
  });

  it("ignores a data-telemetry-id that does not look like a catalog key", () => {
    expect(deriveKey(element('<button data-telemetry-id="Ali Veli">Kaydet</button>'), "/x", names)).toBe("t:system.save");
  });

  it.each([
    ["/orders/3f2c1b0a-1111-2222-3333-444455556666/lines/42", "/orders/:id/lines/:n"],
    ["/Kasa/Masa 7?token=abc#x", "/kasa/masa-:n"],
  ])("builds the route template of %s", (path, expected) => {
    expect(routeTemplate(path)).toBe(expected);
  });

  it("accepts only catalog-shaped keys", () => {
    expect(isCatalogKey("t:system.save")).toBe(true);
    expect(isCatalogKey("auto:button@/a/:n")).toBe(true);
    expect(isCatalogKey("Kaydet")).toBe(false);
    expect(isCatalogKey("ali@example.com")).toBe(false);
    expect(isCatalogKey("a".repeat(65))).toBe(false);
  });
});
