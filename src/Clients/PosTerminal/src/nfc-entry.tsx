import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { NfcOrder } from "./routes/NfcOrder";

/**
 * V1-RMD-141. The dedicated entry point `nfc.html`/`vite.nfc.config.ts`
 * build - mounts `NfcOrder` directly, with no `App.tsx` route dispatch and
 * so no import of Cashier/RelaySettings/ReservationStation at all. Unlike
 * `main.tsx`, deliberately does NOT import `./styles.css` (the shared
 * design-system reset every staff screen relies on) - `NfcOrder.tsx`'s own
 * `nfc-order.css` is already fully self-contained (its own local `--nfc-*`
 * tokens, its own box-sizing reset), exactly so this page never needs to
 * pull in the staff bundle's shared styling.
 */
const root = document.getElementById("root");
if (!root) throw new Error("Root element is missing.");

createRoot(root).render(
  <StrictMode>
    <NfcOrder />
  </StrictMode>,
);
