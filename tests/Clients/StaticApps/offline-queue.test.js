// @vitest-environment jsdom

import { describe, expect, it } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const modulePath = resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/js/offline-queue.js");

const { sortQueueByPriority } = await import(modulePath);

function round(id, { courseNumbers = [], queuedAt } = {}) {
  return {
    id,
    queuedAt,
    items: courseNumbers.map((courseNumber) => ({ courseNumber })),
  };
}

// A fixed "now" close to every fixture's queuedAt keeps these tests from
// depending on the wall clock (sortQueueByPriority defaults `now` to
// Date.now()) and from ever tripping the aging boost below.
const NOW = new Date("2026-09-15T10:00:10.000Z").getTime();

describe("sortQueueByPriority", () => {
  it("sends a starter round before a dessert round queued earlier", () => {
    const dessert = round("dessert", { courseNumbers: [3], queuedAt: "2026-09-15T10:00:00.000Z" });
    const starter = round("starter", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });

    const sorted = sortQueueByPriority([dessert, starter], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["starter", "dessert"]);
  });

  it("uses the round's lowest course number when a round mixes courses", () => {
    const mixed = round("mixed", { courseNumbers: [2, 1], queuedAt: "2026-09-15T10:00:00.000Z" });
    const main = round("main-only", { courseNumbers: [2], queuedAt: "2026-09-15T10:00:01.000Z" });

    const sorted = sortQueueByPriority([main, mixed], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["mixed", "main-only"]);
  });

  it("breaks a tie within the same course by queue time, oldest first", () => {
    const later = round("later", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });
    const earlier = round("earlier", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:00.000Z" });

    const sorted = sortQueueByPriority([later, earlier], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["earlier", "later"]);
  });

  it("treats a round with no course-tagged items as course 0, not last", () => {
    const noCourse = round("no-course", { courseNumbers: [], queuedAt: "2026-09-15T10:00:00.000Z" });
    const main = round("main", { courseNumbers: [2], queuedAt: "2026-09-15T09:59:00.000Z" });

    const sorted = sortQueueByPriority([main, noCourse], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["no-course", "main"]);
  });

  it("does not mutate the original queue array", () => {
    const queue = [
      round("b", { courseNumbers: [2], queuedAt: "2026-09-15T10:00:00.000Z" }),
      round("a", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:00.000Z" }),
    ];
    const originalOrder = queue.map((r) => r.id);

    sortQueueByPriority(queue, NOW);

    expect(queue.map((r) => r.id)).toEqual(originalOrder);
  });

  it("boosts a round that has aged past the starvation threshold ahead of a fresher, lower-course round", () => {
    // "dessert" was queued 3 minutes before "now" (past the 2-minute aging
    // threshold) and would otherwise keep losing to every newly-queued
    // starter round forever, per the bug an independent audit found: a
    // low-priority round re-sorted to the back on every flushQueue() call,
    // with no aging term, can be perpetually re-buried by newer arrivals
    // whenever something ahead of it keeps hitting a transient failure.
    const dessert = round("dessert", { courseNumbers: [3], queuedAt: "2026-09-15T09:57:00.000Z" });
    const freshStarter = round("fresh-starter", { courseNumbers: [1], queuedAt: "2026-09-15T10:00:05.000Z" });

    const sorted = sortQueueByPriority([freshStarter, dessert], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["dessert", "fresh-starter"]);
  });

  it("orders two aged rounds by queuedAt, oldest first", () => {
    const olderAged = round("older-aged", { courseNumbers: [3], queuedAt: "2026-09-15T09:56:00.000Z" });
    const newerAged = round("newer-aged", { courseNumbers: [1], queuedAt: "2026-09-15T09:57:30.000Z" });

    const sorted = sortQueueByPriority([newerAged, olderAged], NOW);

    expect(sorted.map((r) => r.id)).toEqual(["older-aged", "newer-aged"]);
  });
});
