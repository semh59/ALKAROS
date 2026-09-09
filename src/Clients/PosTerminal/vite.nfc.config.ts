import { defineConfig } from "vite";

/**
 * V1-RMD-141. A second, standalone build of just the NFC customer page
 * (nfc.html -> src/nfc-entry.tsx -> NfcOrder.tsx), producing its own
 * isolated dist-nfc/ output the api container serves directly over
 * loopback (DualScreenOptions.NfcWebRoot) - the Cloudflare Tunnel connector
 * reaches that process over loopback and never goes through `web` (Caddy),
 * the same reasoning V12-CWB-001 already established for QR's own
 * --qr-web-root. `base: "/nfc/"` matches the URL prefix the physical NFC
 * tags themselves already encode (/nfc/{tableId}) - existing tags are not
 * re-encoded by this change.
 */
export default defineConfig({
  base: "/nfc/",
  build: {
    outDir: "dist-nfc",
    emptyOutDir: true,
    rollupOptions: {
      input: "nfc.html",
    },
  },
});
