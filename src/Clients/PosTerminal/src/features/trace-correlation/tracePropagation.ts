const API_PREFIX = "/api/";
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const TRACE_ID = /^[0-9a-f]{32}$/;

export interface TraceTarget {
  fetch: typeof fetch;
  location: { origin: string };
}

type TraceListener = (traceId: string) => void;
const listeners = new Set<TraceListener>();

/** Tells the listener the trace id of every API call the app makes; returns a function that stops it. */
export function onTraceIssued(listener: TraceListener): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

const announce = (traceId: string) => listeners.forEach((listener) => listener(traceId));

const randomHex = (bytes: number) =>
  Array.from(crypto.getRandomValues(new Uint8Array(bytes)), (value) => value.toString(16).padStart(2, "0")).join("");

/** The correlation id the app already sends (a UUID) becomes the trace id, so both headers name the same action. */
export function traceIdFrom(correlationId: string | null): string {
  const candidate = correlationId?.replaceAll("-", "").toLowerCase() ?? "";
  const valid = (UUID.test(correlationId ?? "") || TRACE_ID.test(candidate)) && /[1-9a-f]/.test(candidate);
  return valid ? candidate : randomHex(16);
}

function isSameOriginApi(input: RequestInfo | URL, origin: string): boolean {
  const raw = input instanceof Request ? input.url : input.toString();
  const url = new URL(raw, origin);
  return url.origin === origin && url.pathname.startsWith(API_PREFIX);
}

/**
 * Adds a W3C traceparent header (and X-Correlation-Id when missing) to every same-origin API call, so one button
 * press is one trace on the server. Calls to other origins and calls that already carry a traceparent are untouched.
 */
export function installTracePropagation(target: TraceTarget = window): void {
  const original = target.fetch.bind(target);
  target.fetch = (input, init) => {
    if (!isSameOriginApi(input, target.location.origin)) return original(input, init);
    const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
    const existing = headers.get("traceparent");
    if (existing) {
      const given = existing.split("-")[1] ?? "";
      if (TRACE_ID.test(given)) announce(given);
      return original(input, init);
    }
    const correlationId = headers.get("X-Correlation-Id");
    const traceId = traceIdFrom(correlationId);
    if (!correlationId) headers.set("X-Correlation-Id", traceId);
    headers.set("traceparent", `00-${traceId}-${randomHex(8)}-01`);
    announce(traceId);
    return original(input, { ...init, headers });
  };
}
