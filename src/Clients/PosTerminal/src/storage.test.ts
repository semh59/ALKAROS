// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { savedId, tryGetItem, trySetItem } from "./storage";

/**
 * V1-RMD-350 (independent 2026-09-26 audit, a low-severity finding): every
 * localStorage call here used to be unguarded - a private tab, a disabled
 * storage setting, or a full quota throws synchronously from real
 * localStorage, and savedId() is called via useState(() => savedId(...))
 * at the top of nearly every PosTerminal route, so an uncaught throw here
 * crashed the whole screen at mount, not just failed to persist an id.
 */
describe("storage", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("savedId returns and persists a fresh id when none is saved yet", () => {
    const id = savedId("alkaros.test-id");
    expect(id).toMatch(/^[0-9a-f-]{36}$/);
    expect(localStorage.getItem("alkaros.test-id")).toBe(id);
  });

  it("savedId returns the same id on a second call", () => {
    const first = savedId("alkaros.test-id");
    const second = savedId("alkaros.test-id");
    expect(second).toBe(first);
  });

  it("savedId never throws even when localStorage.getItem throws (a private tab)", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new DOMException("SecurityError");
    });

    expect(() => savedId("alkaros.test-id")).not.toThrow();
  });

  it("savedId never throws even when localStorage.setItem throws (a full quota)", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("QuotaExceededError");
    });

    const id = savedId("alkaros.test-id");
    expect(id).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("trySetItem/tryGetItem never throw even when the underlying storage does", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("QuotaExceededError");
    });
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new DOMException("SecurityError");
    });

    expect(() => trySetItem("k", "v")).not.toThrow();
    expect(tryGetItem("k")).toBeNull();
  });
});
