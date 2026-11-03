export const formatMoney = (value: number, currency = "TRY") =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);

export const formatQuantity = (value: number) =>
  new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 }).format(value);

export const formatClockTime = (value: string | Date) =>
  new Date(value).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", second: "2-digit" });

export const formatDecimal = (value: number, digits = 1) =>
  new Intl.NumberFormat("tr-TR", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);

export const formatGigabytes = (bytes: number) => `${formatDecimal(bytes / 1024 ** 3)} GB`;
