import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { installBrowserErrorReporting } from "./features/error-reporting";
import { installTracePropagation } from "./features/trace-correlation";
import "./styles.css";

installBrowserErrorReporting();
installTracePropagation();

const root = document.getElementById("root");
if (!root) throw new Error("Root element is missing.");

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
