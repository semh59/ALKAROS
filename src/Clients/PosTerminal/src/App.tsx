import { RouterProvider } from "./router";
import { Cashier } from "./routes/Cashier";
import { CustomerDisplay } from "./routes/CustomerDisplay";
import { ReservationStation } from "./routes/ReservationStation";

export function App() {
  if (window.location.pathname.startsWith("/display")) {
    return <CustomerDisplay />;
  }
  if (window.location.pathname.startsWith("/reservations")) {
    return <ReservationStation />;
  }
  return (
    <RouterProvider>
      <Cashier />
    </RouterProvider>
  );
}
