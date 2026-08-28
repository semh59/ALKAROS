import type { DisplaySnapshot } from "./contracts";

export interface DisplayFreshness {
  snapshot: DisplaySnapshot | null;
  lastSuccessAt: number | null;
  connectionLost: boolean;
}

export type DisplayPresentation = "idle" | "active" | "paying" | "completed" | "unavailable";

export function displayPresentation(snapshot: DisplaySnapshot): DisplayPresentation {
  return snapshot.state.toLocaleLowerCase("en-US") as DisplayPresentation;
}

export function exposesMoney(presentation: DisplayPresentation): boolean {
  return presentation === "active" || presentation === "paying";
}

export function afterSnapshot(snapshot: DisplaySnapshot, now: number): DisplayFreshness {
  return { snapshot, lastSuccessAt: now, connectionLost: false };
}

export function afterFailure(previous: DisplayFreshness, now: number): DisplayFreshness {
  const stale = previous.lastSuccessAt === null || now - previous.lastSuccessAt >= 10_000;
  return {
    snapshot: stale ? null : previous.snapshot,
    lastSuccessAt: previous.lastSuccessAt,
    connectionLost: true,
  };
}
