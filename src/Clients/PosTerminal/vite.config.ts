import { defineConfig } from "vite";

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
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
