import { Suspense, lazy } from "react";
import { RouterProvider } from "./router";

// V1-RMD-139: found by an independent audit (2026-09-09) — every route below
// used to be a plain top-level import, so Vite bundled all five into one
// entry chunk regardless of which route a visitor actually landed on. A
// customer's phone hitting /nfc/{tableId} downloaded the full Cashier/
// RelaySettings/ReservationStation code along with it — nothing it could
// ever use, and needless surface exposure (route names, API call shapes) on
// an anonymous, unauthenticated device. React.lazy + Suspense makes each
// route its own chunk instead; a visitor only ever fetches the one they
// actually navigate to. Every route file uses a named export (no default),
// hence the `.then(m => ({ default: m.X }))` adapter React.lazy requires.
const Cashier = lazy(() => import("./routes/Cashier").then((m) => ({ default: m.Cashier })));
const CustomerDisplay = lazy(() =>
  import("./routes/CustomerDisplay").then((m) => ({ default: m.CustomerDisplay })),
);
const NfcOrder = lazy(() => import("./routes/NfcOrder").then((m) => ({ default: m.NfcOrder })));
const RelaySettings = lazy(() =>
  import("./routes/RelaySettings").then((m) => ({ default: m.RelaySettings })),
);
const ReservationStation = lazy(() =>
  import("./routes/ReservationStation").then((m) => ({ default: m.ReservationStation })),
);
const CustomerDisplayScreensaverSettings = lazy(() =>
  import("./routes/CustomerDisplayScreensaverSettings").then((m) => ({ default: m.CustomerDisplayScreensaverSettings })),
);

export function App() {
  if (window.location.pathname.startsWith("/display")) {
    return (
      <Suspense fallback={null}>
        <CustomerDisplay />
      </Suspense>
    );
  }
  if (window.location.pathname.startsWith("/reservations")) {
    return (
      <Suspense fallback={null}>
        <ReservationStation />
      </Suspense>
    );
  }
  if (window.location.pathname.startsWith("/nfc/")) {
    return (
      <Suspense fallback={null}>
        <NfcOrder />
      </Suspense>
    );
  }
  if (window.location.pathname.startsWith("/settings/relay")) {
    return (
      <Suspense fallback={null}>
        <RelaySettings />
      </Suspense>
    );
  }
  if (window.location.pathname.startsWith("/settings/screensaver")) {
    return (
      <Suspense fallback={null}>
        <CustomerDisplayScreensaverSettings />
      </Suspense>
    );
  }
  return (
    <Suspense fallback={null}>
      <RouterProvider>
        <Cashier />
      </RouterProvider>
    </Suspense>
  );
}
