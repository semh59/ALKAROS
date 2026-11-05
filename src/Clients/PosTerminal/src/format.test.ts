import { afterEach, describe, expect, it, vi } from "vitest";
import { formatClockMinutes, formatClockTime, formatDate, formatDateTime, formatDayMonthTime, formatShortDateTime, businessDate, businessDateRange, businessDayStart, venueDateRange, venueDateTimeInput, venueInstant, formatDecimal, formatGigabytes } from "./format";

describe("format helpers", () => {
  const deviceZone = process.env.TZ;
  afterEach(() => { if (deviceZone === undefined) delete process.env.TZ; else process.env.TZ = deviceZone; });

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

  it("writes the short date-time, the date and the day-month time in Istanbul time whatever the device zone is", () => {
    process.env.TZ = "UTC";
    expect(formatShortDateTime("2026-09-24T21:30:00Z")).toBe("25.09.2026 00:30");
    expect(formatDate("2026-09-24T21:30:00Z")).toBe("25.09.2026");
    expect(formatDayMonthTime("2026-09-24T21:30:00Z")).toBe("25/09 00:30");
  });

  it.each([
    ["2026-09-24T02:59:59Z", "2026-09-23"],
    ["2026-09-24T03:00:00Z", "2026-09-24"],
    ["2026-09-24T20:59:59Z", "2026-09-24"],
    ["2026-09-24T21:00:00Z", "2026-09-24"],
    ["2026-12-31T23:00:00Z", "2026-12-31"],
    ["2027-01-01T02:59:59Z", "2026-12-31"],
    ["2027-01-01T03:00:00Z", "2027-01-01"],
  ])("the business day at %s is %s (06:00 Istanbul cutoff)", (instant, day) => {
    process.env.TZ = "UTC";
    expect(businessDate(new Date(instant))).toBe(day);
  });

  it("starts the business day at 06:00 Istanbul, whatever the device zone", () => {
    for (const zone of ["UTC", "America/Los_Angeles", "Asia/Tokyo"]) {
      process.env.TZ = zone;
      expect(businessDayStart(new Date("2026-09-24T02:00:00Z"))).toBe("2026-09-23T03:00:00.000Z");
      expect(businessDayStart(new Date("2026-09-24T12:00:00Z"))).toBe("2026-09-24T03:00:00.000Z");
    }
  });

  it("builds an inclusive day range ending on the business day", () => {
    for (const zone of ["UTC", "Asia/Tokyo"]) {
      process.env.TZ = zone;
      expect(businessDateRange(7, new Date("2028-03-02T04:00:00Z"))).toEqual({ from: "2028-02-25", to: "2028-03-02" });
      expect(businessDateRange(7, new Date("2028-03-02T02:00:00Z"))).toEqual({ from: "2028-02-24", to: "2028-03-01" });
    }
    process.env.TZ = "UTC";
    expect(businessDateRange(1, new Date("2026-03-02T04:00:00Z"))).toEqual({ from: "2026-03-02", to: "2026-03-02" });
    expect(businessDateRange(30, new Date("2026-03-02T04:00:00Z"))).toEqual({ from: "2026-02-01", to: "2026-03-02" });
    expect(businessDateRange(7, new Date("2026-03-02T01:00:00Z"))).toEqual({ from: "2026-02-23", to: "2026-03-01" });
    expect(businessDateRange(7, new Date("2026-03-02T04:00:00Z"))).toEqual({ from: "2026-02-24", to: "2026-03-02" });
  });

  it("builds a calendar-day range in the venue zone whatever the device zone", () => {
    for (const zone of ["UTC", "America/Los_Angeles"]) {
      process.env.TZ = zone;
      expect(venueDateRange(7, new Date("2026-09-23T20:59:59Z"))).toEqual({ from: "2026-09-17", to: "2026-09-23" });
      expect(venueDateRange(7, new Date("2026-09-23T21:00:00Z"))).toEqual({ from: "2026-09-18", to: "2026-09-24" });
      expect(venueDateRange(7, new Date("2026-09-24T02:00:00Z"))).toEqual({ from: "2026-09-18", to: "2026-09-24" });
      expect(venueDateRange(7, new Date("2026-12-31T21:00:00Z"))).toEqual({ from: "2026-12-26", to: "2027-01-01" });
      expect(venueDateRange(7, new Date("2028-03-01T10:00:00Z"))).toEqual({ from: "2028-02-24", to: "2028-03-01" });
    }
  });

  it("shows an instant as the venue wall clock for a date-time field and reads the field back as the same instant", () => {
    for (const zone of ["UTC", "America/Los_Angeles", "Asia/Tokyo"]) {
      process.env.TZ = zone;
      expect(venueDateTimeInput(new Date("2026-01-01T10:00:00Z"))).toBe("2026-01-01T13:00");
      expect(venueDateTimeInput(new Date("2026-06-30T21:30:00Z"))).toBe("2026-07-01T00:30");
      expect(venueInstant("2026-01-01T13:00")).toBe("2026-01-01T10:00:00.000Z");
      expect(venueInstant("2026-01-01T13:00:30")).toBe("2026-01-01T10:00:00.000Z");
      expect(venueInstant("2026-07-01T00:30")).toBe("2026-06-30T21:30:00.000Z");
    }
  });

  it("reads the real clock by default", () => {
    process.env.TZ = "UTC";
    vi.useFakeTimers({ toFake: ["Date"] });
    try {
      vi.setSystemTime(new Date("2026-09-24T02:00:00Z"));
      expect(businessDate()).toBe("2026-09-23");
    } finally {
      vi.useRealTimers();
    }
  });

  it("uses a decimal comma", () => {
    expect(formatDecimal(12.34)).toBe("12,3");
    expect(formatDecimal(4.5, 2)).toBe("4,50");
  });

  it("renders bytes as gigabytes with a decimal comma", () => {
    expect(formatGigabytes(775 * 1024 ** 3)).toBe("775,0 GB");
  });
});
