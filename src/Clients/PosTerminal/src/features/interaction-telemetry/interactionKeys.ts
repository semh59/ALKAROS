import * as strings from "../../strings";

const MAX_KEY = 64;
const KEY_SHAPE = /^[a-z0-9][a-z0-9._:-]*(@\/[a-z0-9._:/-]*)?$/;
const UUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

export type NameTable = ReadonlyMap<string, string>;

const normalize = (text: string) => text.replace(/\s+/g, " ").trim();

export const isCatalogKey = (key: string) => key.length <= MAX_KEY && KEY_SHAPE.test(key);

/** Replaces ids and numbers in a path so the same screen always yields the same route key. */
export function routeTemplate(path: string): string {
  const templated = path.split(/[?#]/)[0]!.replace(UUID, ":id").replace(/\d+/g, ":n").toLowerCase();
  const safe = templated.replace(/[^a-z0-9._:/-]/g, "-");
  return safe.startsWith("/") ? safe : `/${safe}`;
}

/**
 * Maps the text a control shows to the dictionary path of that text (for example "t:managementtext.system.resolve").
 * The text is only used to look the key up in the browser; it is never stored or sent.
 */
export function buildNameTable(source: Record<string, unknown> = strings): NameTable {
  const table = new Map<string, string>();
  const walk = (value: unknown, path: string[]) => {
    if (typeof value === "string") {
      const text = normalize(value);
      const key = `t:${path.join(".")}`.toLowerCase().replace(/[^a-z0-9._:-]/g, "-").slice(0, MAX_KEY);
      if (text && !table.has(text) && isCatalogKey(key)) table.set(text, key);
    } else if (value && typeof value === "object") {
      for (const [name, child] of Object.entries(value)) walk(child, [...path, name]);
    }
  };
  walk(source, []);
  return table;
}

/** Order: an explicit data-telemetry-id, then the dictionary key of the control's text, then a route-level automatic key. */
export function deriveKey(element: Element, route: string, names: NameTable): string {
  const marked = element.closest("[data-telemetry-id]")?.getAttribute("data-telemetry-id");
  if (marked && isCatalogKey(marked)) return marked;
  const name = normalize(element.getAttribute("aria-label") ?? element.textContent ?? "");
  const mapped = name ? names.get(name) : undefined;
  if (mapped) return mapped;
  return `auto:${element.tagName.toLowerCase()}@${route}`.slice(0, MAX_KEY);
}
