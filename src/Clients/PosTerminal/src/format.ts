export const formatMoney = (value: number, currency = "TRY") =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);

export const formatQuantity = (value: number) =>
  new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 }).format(value);

// The venue's clock, not the device's: a till with a wrong zone must still show the time the guest was served.
const venueTimeZone = "Europe/Istanbul";

export const formatClockTime = (value: string | Date) =>
  new Date(value).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", second: "2-digit", timeZone: venueTimeZone });

export const formatClockMinutes = (value: string | Date) =>
  new Date(value).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", timeZone: venueTimeZone });

export const formatDateTime = (value: string | Date) =>
  new Date(value).toLocaleString("tr-TR", { timeZone: venueTimeZone });

export const formatShortDateTime = (value: string | Date) =>
  new Date(value).toLocaleString("tr-TR", { dateStyle: "short", timeStyle: "short", timeZone: venueTimeZone });

export const formatDayMonthTime = (value: string | Date) =>
  new Date(value).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", timeZone: venueTimeZone });

export const formatDate = (value: string | Date) =>
  new Date(value).toLocaleDateString("tr-TR", { timeZone: venueTimeZone });

// Mirrors the server's business day (BusinessDayWindow): 06:00 Istanbul to the next 06:00. Turkey has no daylight saving, so the offset is fixed.
const istanbulOffsetHours = 3;
const businessDayStartHour = 6;
const hourMs = 3_600_000;
const dayMs = 24 * hourMs;

/** The business day an instant belongs to, as yyyy-mm-dd. */
export const businessDate = (now: Date = new Date()) =>
  new Date(now.getTime() - (businessDayStartHour - istanbulOffsetHours) * hourMs).toISOString().slice(0, 10);

/** The UTC instant the current business day began. */
export const businessDayStart = (now: Date = new Date()) =>
  new Date(Date.parse(`${businessDate(now)}T00:00:00Z`) + (businessDayStartHour - istanbulOffsetHours) * hourMs).toISOString();

/** The last `days` business days (inclusive), as yyyy-mm-dd bounds. */
export const businessDateRange = (days: number, now: Date = new Date()) => {
  const to = businessDate(now);
  return { from: new Date(Date.parse(`${to}T00:00:00Z`) - (days - 1) * dayMs).toISOString().slice(0, 10), to };
};

/** The last `days` calendar days (inclusive) in the venue zone, for screens keyed by a tax date rather than the business day. */
export const venueDateRange = (days: number, now: Date = new Date()) => {
  const to = now.toLocaleDateString("sv-SE", { timeZone: venueTimeZone });
  return { from: new Date(Date.parse(`${to}T00:00:00Z`) - (days - 1) * dayMs).toISOString().slice(0, 10), to };
};

export const formatDecimal = (value: number, digits = 1) =>
  new Intl.NumberFormat("tr-TR", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);

export const formatGigabytes = (bytes: number) => `${formatDecimal(bytes / 1024 ** 3)} GB`;
