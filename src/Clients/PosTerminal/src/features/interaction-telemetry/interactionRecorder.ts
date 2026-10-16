export type InteractionKind = "click" | "navigate" | "submit";
export type InteractionOutcome = "ok" | "error" | "cancelled";

export interface InteractionRecord {
  readonly key: string;
  readonly kind: InteractionKind;
  readonly atMs: number;
  readonly durationMs: number;
  readonly longTasks: number;
  readonly shiftMilli: number;
  readonly outcome: InteractionOutcome;
}

export const ENDPOINT = "/api/v1/telemetry/interactions";
export const STORAGE_KEY = "alkaros.telemetry.interactions.v1";

export interface RecorderOptions {
  readonly send?: (body: string) => boolean;
  readonly storage?: Pick<Storage, "getItem" | "setItem">;
  readonly now?: () => number;
  readonly maxQueue?: number;
  readonly batchSize?: number;
  readonly maxStoredChars?: number;
}

/**
 * Keeps interactions in a small queue of its own (its own storage key and size limit, so it can never crowd out
 * the order queue), and sends them in batches. When sending fails the records stay for the next try.
 */
export function createInteractionRecorder(options: RecorderOptions = {}) {
  const send = options.send ?? ((body) => navigator.sendBeacon(ENDPOINT, new Blob([body], { type: "application/json" })));
  const storage = options.storage ?? globalThis.localStorage;
  const now = options.now ?? (() => Date.now());
  const maxQueue = options.maxQueue ?? 200;
  const batchSize = options.batchSize ?? 50;
  const maxStoredChars = options.maxStoredChars ?? 20_000;
  let queue: InteractionRecord[] = [];
  let storageFailed = false;

  function persist() {
    try {
      let text = JSON.stringify(queue);
      while (text.length > maxStoredChars && queue.length > 0) {
        queue = queue.slice(Math.ceil(queue.length / 4));
        text = JSON.stringify(queue);
      }
      storage.setItem(STORAGE_KEY, text);
    } catch {
      storageFailed = true;
    }
  }

  function restore() {
    try {
      const parsed: unknown = JSON.parse(storage.getItem(STORAGE_KEY) ?? "[]");
      if (Array.isArray(parsed)) queue = (parsed as InteractionRecord[]).slice(-maxQueue);
    } catch {
      queue = [];
    }
  }

  function record(item: InteractionRecord) {
    queue.push(item);
    if (queue.length > maxQueue) queue = queue.slice(queue.length - maxQueue);
    persist();
  }

  function flush(): boolean {
    while (queue.length > 0) {
      const batch = queue.slice(0, batchSize);
      const at = now();
      const items = batch.map((item) => ({
        key: item.key,
        kind: item.kind,
        ageMs: Math.max(0, at - item.atMs),
        durationMs: item.durationMs,
        longTasks: item.longTasks,
        shiftMilli: item.shiftMilli,
        outcome: item.outcome,
      }));
      if (!send(JSON.stringify({ items }))) return false;
      queue = queue.slice(batch.length);
      persist();
    }
    return true;
  }

  restore();
  return { record, flush, size: () => queue.length, storageFailed: () => storageFailed };
}
