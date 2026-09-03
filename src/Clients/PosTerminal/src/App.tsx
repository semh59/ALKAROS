import { RouterProvider } from "./router";
import { Cashier } from "./routes/Cashier";
import { CustomerDisplay } from "./routes/CustomerDisplay";

export function App() {
  if (window.location.pathname.startsWith("/display")) {
    return <CustomerDisplay />;
  }
  return (
    <RouterProvider>
      <Cashier />
    </RouterProvider>
  );
}
