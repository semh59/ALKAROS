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

/** The venue's current calendar day as yyyy-mm-dd, for a business-day field's default. */
export const venueToday = () => new Date().toLocaleDateString("sv-SE", { timeZone: venueTimeZone });

export const formatDecimal = (value: number, digits = 1) =>
  new Intl.NumberFormat("tr-TR", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);

export const formatGigabytes = (bytes: number) => `${formatDecimal(bytes / 1024 ** 3)} GB`;
