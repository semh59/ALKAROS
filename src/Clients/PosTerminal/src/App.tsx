import { RouterProvider } from "./router";
import { Cashier } from "./routes/Cashier";
import { CustomerDisplay } from "./routes/CustomerDisplay";
import { NfcOrder } from "./routes/NfcOrder";
import { RelaySettings } from "./routes/RelaySettings";
import { ReservationStation } from "./routes/ReservationStation";

export function App() {
  if (window.location.pathname.startsWith("/display")) {
    return <CustomerDisplay />;
  }
  if (window.location.pathname.startsWith("/reservations")) {
    return <ReservationStation />;
  }
  if (window.location.pathname.startsWith("/nfc/")) {
    return <NfcOrder />;
  }
  if (window.location.pathname.startsWith("/settings/relay")) {
    return <RelaySettings />;
  }
  return (
    <RouterProvider>
      <Cashier />
    </RouterProvider>
  );
}
