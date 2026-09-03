import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

/**
 * A minimal history router for the cashier surface. Navigation between the
 * sales screen and the workspace routes is an in-app state change
 * (history.pushState + a re-render), not a full document load — so the
 * session is not re-validated, the catalog is not re-fetched and an open
 * draft is not at risk on every transition (deep-analysis finding F-6).
 * The customer display is a separate window and does not use this.
 */

const normalize = (path: string) => path.replace(/\/+$/, "") || "/";

interface RouterValue {
  path: string;
  navigate: (to: string) => void;
}

const RouterContext = createContext<RouterValue | null>(null);

export function RouterProvider({ children }: { children: ReactNode }) {
  const [path, setPath] = useState(() => normalize(window.location.pathname));

  useEffect(() => {
    const sync = () => setPath(normalize(window.location.pathname));
    window.addEventListener("popstate", sync);
    return () => window.removeEventListener("popstate", sync);
  }, []);

  const navigate = useCallback((to: string) => {
    const next = normalize(to);
    if (next === normalize(window.location.pathname)) return;
    window.history.pushState(null, "", next);
    setPath(next);
  }, []);

  const value = useMemo<RouterValue>(() => ({ path, navigate }), [path, navigate]);
  return <RouterContext.Provider value={value}>{children}</RouterContext.Provider>;
}

export function useRouter(): RouterValue {
  const value = useContext(RouterContext);
  if (value === null) {
    throw new Error("useRouter must be used inside a <RouterProvider>.");
  }
  return value;
}

/**
 * True for a plain left-click the router should handle. Modifier clicks and
 * middle-clicks fall through to the browser so "open in new tab" keeps working.
 */
export function isPlainClick(event: {
  defaultPrevented: boolean;
  button: number;
  metaKey: boolean;
  ctrlKey: boolean;
  shiftKey: boolean;
  altKey: boolean;
}): boolean {
  return (
    !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey
  );
}
