import { describe, expect, it } from "vitest";
import { formatClockMinutes, formatClockTime, formatDateTime, formatDecimal, formatGigabytes } from "./format";

describe("format helpers", () => {
  it("renders an ISO instant as a Turkish local clock time, never the raw ISO text", () => {
    const text = formatClockTime("2026-11-01T08:02:49.436Z");
    expect(text).toMatch(/^\d{2}:\d{2}:\d{2}$/);
    expect(text).not.toContain("T");
  });

  it("shows the venue's Istanbul time, not the device's", () => {
    process.env.TZ = "UTC";
    expect(formatClockTime("2026-11-01T08:02:49.436Z")).toBe("11:02:49");
    expect(formatClockMinutes("2026-09-24T11:32:00Z")).toBe("14:32");
    expect(formatDateTime("2026-09-24T21:30:00Z")).toBe("25.09.2026 00:30:00");
  });

  it("uses a decimal comma", () => {
    expect(formatDecimal(12.34)).toBe("12,3");
    expect(formatDecimal(4.5, 2)).toBe("4,50");
  });

  it("renders bytes as gigabytes with a decimal comma", () => {
    expect(formatGigabytes(775 * 1024 ** 3)).toBe("775,0 GB");
  });
});
