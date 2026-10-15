const ENDPOINT = "/api/v1/telemetry/browser-errors";
const MAX_PER_KEY = 3;
const MAX_TOTAL = 20;

export type BrowserErrorKind = "js_error" | "unhandled_rejection" | "network_error" | "render_error";

type Beacon = (url: string, data?: BodyInit | null) => boolean;

export interface ReporterOptions {
  readonly send?: Beacon;
  readonly route?: () => string;
  readonly newId?: () => string;
}

/**
 * Reports uncaught browser errors to the server. Only a fixed kind and the page route leave the browser: never
 * the message, the stack, typed values or tokens. The same error is reported at most three times per page load.
 */
export function createBrowserErrorReporter(options: ReporterOptions = {}) {
  const send: Beacon = options.send ?? ((url, data) => navigator.sendBeacon(url, data));
  const route = options.route ?? (() => window.location.pathname);
  const newId = options.newId ?? (() => crypto.randomUUID());
  const counts = new Map<string, number>();
  let total = 0;

  function report(kind: BrowserErrorKind, key: string): boolean {
    const dedupeKey = `${kind}:${key}`;
    const seen = counts.get(dedupeKey) ?? 0;
    if (seen >= MAX_PER_KEY || total >= MAX_TOTAL) {
      return false;
    }
    counts.set(dedupeKey, seen + 1);
    total += 1;
    const body = new Blob([JSON.stringify({ eventId: newId(), kind, route: route() })], { type: "application/json" });
    return send(ENDPOINT, body);
  }

  return { report };
}

/** Installs the global handlers; returns a function that removes them again. */
export function installBrowserErrorReporting(options: ReporterOptions = {}): () => void {
  const reporter = createBrowserErrorReporter(options);
  const onError = (event: ErrorEvent) => {
    reporter.report("js_error", `${event.filename}:${event.lineno}:${event.colno}`);
  };
  const onRejection = (event: PromiseRejectionEvent) => {
    reporter.report("unhandled_rejection", String(event.reason?.name ?? "rejection"));
  };
  window.addEventListener("error", onError);
  window.addEventListener("unhandledrejection", onRejection);
  return () => {
    window.removeEventListener("error", onError);
    window.removeEventListener("unhandledrejection", onRejection);
  };
}
