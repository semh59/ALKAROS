import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { defineConfig } from "vitest/config";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

export default defineConfig({
  test: {
    environment: "jsdom",
    include: ["**/*.test.js"],
    setupFiles: [resolve(here, "support/setup.js")],
    // Every test loads the app under test from scratch (vi.resetModules), and the first load of a file pays the whole
    // module-graph transform. That cost grows with CPU contention (measured: first waiter-app test 0.5 s alone, 2.2 s with
    // 24 busy processes; with a 2.5 s limit only those cold-start tests fail), so the 5 s default left no margin on a
    // busy machine. The limits are six times the default and do not hide a slow test: a hung one still fails.
    testTimeout: 30_000,
    hookTimeout: 30_000,
  },
  server: {
    // The apps under test (src/Clients/**/wwwroot/*.js) live outside this
    // package's own directory — Vite's dev server denies file access
    // outside its allowed roots by default.
    fs: { allow: [repoRoot] },
  },
});
