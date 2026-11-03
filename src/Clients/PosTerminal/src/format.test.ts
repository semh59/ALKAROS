import { describe, expect, it } from "vitest";
import { formatClockTime, formatDecimal, formatGigabytes } from "./format";

describe("format helpers", () => {
  it("renders an ISO instant as a Turkish local clock time, never the raw ISO text", () => {
    const text = formatClockTime("2026-11-01T08:02:49.436Z");
    expect(text).toMatch(/^\d{2}:\d{2}:\d{2}$/);
    expect(text).not.toContain("T");
  });

  it("uses a decimal comma", () => {
    expect(formatDecimal(12.34)).toBe("12,3");
    expect(formatDecimal(4.5, 2)).toBe("4,50");
  });

  it("renders bytes as gigabytes with a decimal comma", () => {
    expect(formatGigabytes(775 * 1024 ** 3)).toBe("775,0 GB");
  });
});
