import { Cashier } from "./routes/Cashier";
import { CustomerDisplay } from "./routes/CustomerDisplay";

export function App() {
  return window.location.pathname.startsWith("/display") ? <CustomerDisplay /> : <Cashier />;
}
