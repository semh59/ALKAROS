import { buildNameTable, deriveKey, routeTemplate, type NameTable } from "./interactionKeys";
import { onTraceIssued } from "../trace-correlation";
import { createInteractionRecorder, type InteractionKind, type InteractionRecord } from "./interactionRecorder";

const INTERACTIVE = "button, a[href], [role='button'], [role='tab'], [role='menuitem'], input[type='submit'], input[type='button'], [data-telemetry-id]";
const FLUSH_MS = 10_000;
/** The first API call within this time after a click or submit is taken as the one that click started. */
const TRACE_WINDOW_MS = 2_000;

export interface InstallOptions {
  readonly target?: Window & typeof globalThis;
  readonly recorder?: ReturnType<typeof createInteractionRecorder>;
  readonly names?: NameTable;
  /** Runs the callback after the next paint; replaceable so tests need no real frames. */
  readonly afterPaint?: (callback: () => void) => void;
  /** Reports the trace id of each API call; replaceable so tests need no real fetch. */
  readonly traceSource?: (listener: (traceId: string) => void) => () => void;
}

interface OpenClick { until: number; traceId?: string; record?: InteractionRecord }

/**
 * Records every click on a control, every route change and every form submit as a key, a duration and an outcome.
 * It never reads typed values or keeps the control's text. Returns a function that removes the listeners.
 */
export function installInteractionTelemetry(options: InstallOptions = {}): () => void {
  const target = options.target ?? window;
  const recorder = options.recorder ?? createInteractionRecorder();
  const names = options.names ?? buildNameTable();
  const afterPaint = options.afterPaint ?? ((callback) => target.requestAnimationFrame(() => target.setTimeout(callback, 0)));
  const cleanups: (() => void)[] = [];
  let longTasks = 0;
  let shift = 0;
  let errors = 0;
  let open: OpenClick | undefined;

  cleanups.push((options.traceSource ?? onTraceIssued)((traceId) => {
    if (!open || open.traceId || Date.now() > open.until) return;
    open.traceId = traceId;
    if (open.record) {
      open.record.traceId = traceId;
      recorder.save();
    }
  }));

  function observe(type: string, onEntry: (entry: PerformanceEntry) => void) {
    const PerformanceObserverType = target.PerformanceObserver as typeof PerformanceObserver | undefined;
    if (!PerformanceObserverType?.supportedEntryTypes?.includes(type)) return;
    const observer = new PerformanceObserverType((list) => list.getEntries().forEach(onEntry));
    observer.observe({ type, buffered: false });
    cleanups.push(() => observer.disconnect());
  }

  observe("longtask", () => { longTasks += 1; });
  observe("layout-shift", (entry) => {
    const candidate = entry as PerformanceEntry & { value?: number; hadRecentInput?: boolean };
    if (!candidate.hadRecentInput) shift += candidate.value ?? 0;
  });

  function measure(key: string, kind: InteractionKind, startedAt: number) {
    const wall = Date.now();
    const tasks0 = longTasks;
    const shift0 = shift;
    const errors0 = errors;
    const click: OpenClick = { until: wall + TRACE_WINDOW_MS };
    if (kind !== "navigate") open = click;
    afterPaint(() => {
      const record: InteractionRecord = {
        key,
        kind,
        atMs: wall,
        durationMs: Math.max(0, Math.round(target.performance.now() - startedAt)),
        longTasks: longTasks - tasks0,
        shiftMilli: Math.round((shift - shift0) * 1000),
        outcome: errors > errors0 ? "error" : "ok",
        traceId: click.traceId,
      };
      click.record = record;
      recorder.record(record);
    });
  }

  const route = () => routeTemplate(target.location.pathname);

  function listen<K extends keyof WindowEventMap>(type: K, handler: (event: WindowEventMap[K]) => void, capture = false) {
    target.addEventListener(type, handler, { capture, passive: true });
    cleanups.push(() => target.removeEventListener(type, handler, { capture }));
  }

  listen("click", (event) => {
    const element = (event.target instanceof Element ? event.target : null)?.closest(INTERACTIVE);
    if (element) measure(deriveKey(element, route(), names), "click", event.timeStamp);
  }, true);
  listen("submit", (event) => {
    if (event.target instanceof Element) measure(deriveKey(event.target, route(), names), "submit", event.timeStamp);
  }, true);
  listen("error", () => { errors += 1; });
  listen("unhandledrejection", () => { errors += 1; });
  listen("popstate", () => measure(`nav@${route()}`, "navigate", target.performance.now()));

  const history = target.history;
  const originalPush = history.pushState.bind(history);
  const originalReplace = history.replaceState.bind(history);
  history.pushState = (...args) => {
    const started = target.performance.now();
    originalPush(...args);
    measure(`nav@${route()}`, "navigate", started);
  };
  history.replaceState = (...args) => originalReplace(...args);
  cleanups.push(() => { history.pushState = originalPush; history.replaceState = originalReplace; });

  const timer = target.setInterval(() => recorder.flush(), FLUSH_MS);
  cleanups.push(() => target.clearInterval(timer));
  const hide = () => { if (target.document.visibilityState === "hidden") recorder.flush(); };
  target.document.addEventListener("visibilitychange", hide);
  cleanups.push(() => target.document.removeEventListener("visibilitychange", hide));

  return () => cleanups.forEach((cleanup) => cleanup());
}
