import { describe, expect, it } from "vitest";
import { HEALTH_SNAPSHOT_STALE_AFTER_MS, healthSnapshotStaleMessage, isHealthSnapshotStale } from "./models";

const taken = "2026-10-17T10:00:00Z";
const takenMs = Date.parse(taken);

describe("health snapshot staleness", () => {
  it.each([
    [0, false],
    [HEALTH_SNAPSHOT_STALE_AFTER_MS, false],
    [HEALTH_SNAPSHOT_STALE_AFTER_MS + 1, true],
    [-60_000, false],
  ])("a snapshot %i ms old is stale: %s", (age, stale) => {
    expect(isHealthSnapshotStale(taken, takenMs + age)).toBe(stale);
  });

  it("treats an unreadable timestamp as stale", () => {
    expect(isHealthSnapshotStale("not a date", takenMs)).toBe(true);
  });

  it("names the capture time, or warns that it is unreadable", () => {
    expect(healthSnapshotStaleMessage(taken)).toMatch(/^Son ölçüm .*\d.* Gösterilen değerler güncel olmayabilir\.$/);
    expect(healthSnapshotStaleMessage("not a date")).toContain("zamanı okunamadı");
  });

  it("uses a three minute threshold", () => {
    expect(HEALTH_SNAPSHOT_STALE_AFTER_MS).toBe(180_000);
  });
});
