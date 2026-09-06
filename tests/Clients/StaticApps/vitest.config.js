import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { defineConfig } from "vitest/config";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

export default defineConfig({
  test: {
    environment: "jsdom",
    include: ["**/*.test.js"],
  },
  server: {
    // The apps under test (src/Clients/**/wwwroot/*.js) live outside this
    // package's own directory — Vite's dev server denies file access
    // outside its allowed roots by default.
    fs: { allow: [repoRoot] },
  },
});
