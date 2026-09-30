import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Button, StateMessage } from "../../design-system";
import { commonActions } from "../../strings";
import { SecurityApiError } from "./api";

export const errorText = (reason: unknown, fallback: string) => (reason instanceof SecurityApiError ? reason.message : fallback);

export type Panel<T> = { state: "loading" } | { state: "error"; message: string } | { state: "ready"; data: T };

export function usePanel<T>(load: () => Promise<T>, failed: string): [Panel<T>, () => void] {
  const [panel, setPanel] = useState<Panel<T>>({ state: "loading" });
  const run = useCallback(() => {
    setPanel({ state: "loading" });
    load().then((data) => setPanel({ state: "ready", data }), (reason) => setPanel({ state: "error", message: errorText(reason, failed) }));
  }, [load, failed]);
  useEffect(run, [run]);
  return [panel, run];
}

export function PanelView<T>({ title, loading, failed, panel, onRetry, children }: {
  title: string; loading: string; failed: string; panel: Panel<T>; onRetry: () => void; children: (data: T) => ReactNode;
}) {
  return <section className="msys__panel" aria-label={title}>
    <h3>{title}</h3>
    {panel.state === "loading" && <p aria-busy="true">{loading}</p>}
    {panel.state === "error" && <StateMessage tone="error" title={failed} actions={<Button variant="secondary" onClick={onRetry}>{commonActions.retry}</Button>}><p>{panel.message}</p></StateMessage>}
    {panel.state === "ready" && children(panel.data)}
  </section>;
}

export function Notice({ notice }: { notice: { tone: "success" | "error"; text: string } | undefined }) {
  return notice ? <div role={notice.tone === "error" ? "alert" : "status"} className={`msys__notice msys__notice--${notice.tone}`}>{notice.text}</div> : null;
}
