import { describe, expect, it } from "vitest";
import { createInteractionRecorder, STORAGE_KEY, type InteractionRecord } from "./interactionRecorder";

const item = (key = "t:a.b", atMs = 1_000): InteractionRecord =>
  ({ key, kind: "click", atMs, durationMs: 40, longTasks: 1, shiftMilli: 2, outcome: "ok" });

function memoryStorage(initial: Record<string, string> = {}) {
  const data = new Map(Object.entries(initial));
  return { data, getItem: (k: string) => data.get(k) ?? null, setItem: (k: string, v: string) => { data.set(k, v); } };
}

describe("interaction recorder", () => {
  it("sends batches of at most fifty with the age of each interaction and nothing else", () => {
    const bodies: string[] = [];
    const recorder = createInteractionRecorder({ send: (b) => { bodies.push(b); return true; }, storage: memoryStorage(), now: () => 6_000 });
    for (let i = 0; i < 120; i += 1) recorder.record(item(`t:k.${i}`));

    expect(recorder.flush()).toBe(true);

    expect(bodies).toHaveLength(3);
    const first = JSON.parse(bodies[0]!) as { items: Record<string, unknown>[] };
    expect(first.items).toHaveLength(50);
    expect(Object.keys(first.items[0]!).sort()).toEqual(["ageMs", "durationMs", "key", "kind", "longTasks", "outcome", "shiftMilli"]);
    expect(first.items[0]!.ageMs).toBe(5_000);
    expect(recorder.size()).toBe(0);
  });

  it("keeps everything when sending fails and resends later", () => {
    let online = false;
    const recorder = createInteractionRecorder({ send: () => online, storage: memoryStorage() });
    recorder.record(item());
    recorder.record(item());

    expect(recorder.flush()).toBe(false);
    expect(recorder.size()).toBe(2);
    online = true;
    expect(recorder.flush()).toBe(true);
    expect(recorder.size()).toBe(0);
  });

  it("keeps a bounded queue, dropping the oldest, and survives a reload through its own storage key", () => {
    const storage = memoryStorage();
    const recorder = createInteractionRecorder({ send: () => false, storage, maxQueue: 5 });
    for (let i = 0; i < 9; i += 1) recorder.record(item(`t:k.${i}`));

    expect(recorder.size()).toBe(5);
    const reloaded = createInteractionRecorder({ send: () => false, storage, maxQueue: 5 });
    expect(reloaded.size()).toBe(5);
    expect((JSON.parse(storage.data.get(STORAGE_KEY)!) as InteractionRecord[])[0]!.key).toBe("t:k.4");
  });

  it("a full telemetry queue never touches the order queue or grows past its storage limit", () => {
    const storage = memoryStorage({ "alkaros.order-queue": '[{"order":1}]' });
    const recorder = createInteractionRecorder({ send: () => false, storage, maxQueue: 100_000, maxStoredChars: 2_000 });

    for (let i = 0; i < 5_000; i += 1) recorder.record(item(`t:k.${i}`));

    expect(storage.data.get("alkaros.order-queue")).toBe('[{"order":1}]');
    expect([...storage.data.keys()].sort()).toEqual(["alkaros.order-queue", STORAGE_KEY]);
    expect(storage.data.get(STORAGE_KEY)!.length).toBeLessThanOrEqual(2_000);
  });

  it("a storage that refuses writes never throws into the page", () => {
    const storage = { getItem: () => null, setItem: () => { throw new Error("quota"); } };
    const recorder = createInteractionRecorder({ send: () => true, storage });

    expect(() => recorder.record(item())).not.toThrow();
    expect(recorder.storageFailed()).toBe(true);
    expect(recorder.flush()).toBe(true);
  });

  it("ignores a corrupt stored queue", () => {
    const recorder = createInteractionRecorder({ send: () => true, storage: memoryStorage({ [STORAGE_KEY]: "{not json" }) });

    expect(recorder.size()).toBe(0);
  });
});
