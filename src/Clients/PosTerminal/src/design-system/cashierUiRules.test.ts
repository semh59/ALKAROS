import { existsSync, readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { planLegend } from "../features/tables/tablePlanModel";

const repo = (path: string) => new URL(`../../../../../${path}`, import.meta.url);
const read = (path: string) => readFileSync(repo(path), "utf8");

const doc = read("docs/CASHIER_UI_RULES.md");
const tokens = read("src/Clients/PosTerminal/src/design-system/tokens.css");
const vanillaStyles = [
  read("src/Clients/Cashier/wwwroot/cashier-app.css"),
  read("src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css"),
];

const cells = (line: string) => line.split("|").slice(1, -1).map((cell) => cell.trim());
const section = (heading: string) => {
  const start = doc.indexOf(`## ${heading}`);
  expect(start, `${heading} missing`).toBeGreaterThanOrEqual(0);
  const end = doc.indexOf("\n## ", start + 1);
  return doc.slice(start, end < 0 ? undefined : end);
};
const statusRows = () => section("4. Durum tablosu").split("\n").filter((line) => /^\| `\w+` \|/.test(line)).map(cells);
const backticked = (pattern: RegExp) => [...new Set([...doc.matchAll(/`(--[\w-]+)`/g)].map((match) => match[1]).filter((name) => pattern.test(name)))];

describe("docs/CASHIER_UI_RULES.md", () => {
  it("lists every table-plan status once, with the label and icon the table plan draws", () => {
    const rows = statusRows();
    expect(rows.map(([key]) => key.replaceAll("`", ""))).toEqual(planLegend.map((entry) => entry.key));
    rows.forEach(([, label, icon], index) => {
      expect([label, icon]).toEqual([planLegend[index].label, planLegend[index].icon]);
    });
  });

  it("gives every status a text and a background token that tokens.css defines", () => {
    for (const row of statusRows()) {
      for (const token of [row[3], row[4]]) {
        expect(token, `${row[0]} token`).toMatch(/^`--ds-color-[\w-]+`$/);
        expect(tokens).toContain(`${token.replaceAll("`", "")}:`);
      }
    }
  });

  it("writes the primary button's text in ink on the accent fill, as the button table of the rules says", () => {
    expect(section("5. Düğme hiyerarşisi")).toContain("`--ds-color-accent` dolgu, `--ds-color-ink` yazı");
    const rule = read("src/Clients/PosTerminal/src/design-system/primitives.css").match(/\.ds-button--primary \{([^}]*)\}/)![1];
    expect(rule).toMatch(/(?:^|[;\s])color:\s*var\(--ds-color-ink\)/);
    expect(rule).toMatch(/background:\s*var\(--ds-color-accent\)/);
  });

  it("pairs each status text token with its own soft tint, or with the neutral surface", () => {
    for (const [key, , , text, background] of statusRows()) {
      expect(background, key).toBeOneOf([`${text.slice(0, -1)}-soft\``, "`--ds-color-surface-subtle`"]);
    }
  });

  it("resolves every relative link of the document to an existing file", () => {
    const links = [...doc.matchAll(/\]\(([^)#]+\.md)\)/g)].map((match) => match[1]);
    expect(links).toEqual(expect.arrayContaining(["UI_STYLE_GUIDE.md", "design/foundations.md"]));
    for (const link of links) expect(existsSync(repo(`docs/${link}`)), link).toBe(true);
  });

  it("states for each shared token the value both stylesheets really carry", () => {
    const rows = doc.split("\n").filter((line) => /^\| `--ds-[\w-]+` \| `--[\w-]+` \|/.test(line)).map((line) => cells(line).map((cell) => cell.replaceAll("`", "")));
    expect(rows.length).toBe(9);
    const valueOf = (css: string, name: string) => new RegExp(`${name}:\\s*([^;]+);`).exec(css)?.[1].trim().toLowerCase();
    for (const [ds, vanilla, expected] of rows) {
      expect(valueOf(tokens, ds), ds).toBe(expected);
      for (const css of vanillaStyles) expect(valueOf(css, vanilla), vanilla).toBe(expected);
    }
  });

  it("names only design tokens that exist", () => {
    const names = backticked(/^--ds-/);
    expect(names.length).toBeGreaterThan(10);
    for (const name of names) expect(tokens, name).toContain(`${name}:`);
  });

  it("names the Cashier page tokens only when both Cashier stylesheets define them", () => {
    const names = backticked(/^--(color|touch)-/);
    expect(names.length).toBeGreaterThan(5);
    for (const name of names) for (const css of vanillaStyles) expect(css, name).toContain(`${name}:`);
  });

  it("states the touch target, the gap and the long-open threshold the tokens carry", () => {
    const targetPx = /--ds-target-min:\s*(\d+)px/.exec(tokens)![1];
    const heatMinutes = /--ds-heat-active-max-minutes:\s*(\d+)/.exec(tokens)![1];
    expect(tokens).toMatch(/--ds-space-4:\s*1rem;/);
    expect(section("3. Dokunma hedefi ve boşluk")).toContain(`\`--ds-target-min\` (${targetPx}px)`);
    expect(section("3. Dokunma hedefi ve boşluk")).toContain("en az 16px (`--ds-space-4`)");
    expect(section("4. Durum tablosu")).toContain(`${heatMinutes} dakikayı (\`--ds-heat-active-max-minutes\`)`);
  });

  it("cites only foundations sections that exist", () => {
    const foundations = read("docs/design/foundations.md");
    for (const heading of ["## 3. Boşluk ve dokunma hedefleri", "### 5.2 Onay kalıpları", "### 5.3 Bildirim türleri"]) expect(foundations).toContain(heading);
    expect(doc).toContain("foundations.md) 3).");
    expect(doc).toContain("(foundations 5.2)");
    expect(doc).toContain("(foundations 5.3)");
  });

  it("fixes the smallest text size and the money format", () => {
    expect(section("2. Punto")).toContain("En küçük yazı 14px");
    expect(section("6. Para ve rakam biçimi")).toContain("₺160,00");
  });
});
