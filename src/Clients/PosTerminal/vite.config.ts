import { defineConfig } from "vitest/config";

export default defineConfig({
  server: {
    port: 5173,
    proxy: {
      "/health": process.env.POS_API_PROXY_TARGET ?? "http://127.0.0.1:5080",
      "/api": process.env.POS_API_PROXY_TARGET ?? "http://127.0.0.1:5080",
      "/hubs": {
        target: process.env.POS_API_PROXY_TARGET ?? "http://127.0.0.1:5080",
        ws: true,
      },
    },
  },
  // The screen tests render whole routes in jsdom (about 4.5 s alone) and run in parallel, so the 5 s default flakes under load.
  test: {
    testTimeout: 30_000,
    hookTimeout: 30_000,
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
