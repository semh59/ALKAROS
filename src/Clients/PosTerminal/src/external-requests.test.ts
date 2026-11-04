import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");
const stylesheets = (directory: string): string[] =>
  readdirSync(directory, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory() ? stylesheets(join(directory, entry.name)) : entry.name.endsWith(".css") ? [join(directory, entry.name)] : []);

// A till must open with no internet: nothing a page head or a stylesheet asks for may live on another host.
const otherHost = /(?:^|[\s"'(=,])(?:https?:)?\/\/[a-z0-9]/i;

describe("requests to another host", () => {
  it.each(["../index.html", "../nfc.html"])("%s asks for nothing from another host", (file) => {
    expect(read(file)).not.toMatch(otherHost);
  });

  it("no stylesheet imports or loads anything from another host", () => {
    const offenders = stylesheets(fileURLToPath(new URL(".", import.meta.url)))
      .filter((file) => otherHost.test(readFileSync(file, "utf8").replace(/\/\*[\s\S]*?\*\//g, "")));
    expect(offenders).toEqual([]);
  });
});
