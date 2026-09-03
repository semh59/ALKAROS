export const formatMoney = (value: number, currency = "TRY") =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency }).format(value);

export const formatQuantity = (value: number) =>
  new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 }).format(value);

export const grossUnitPrice = (netUnitPrice: number, taxRate: number) =>
  Math.round((netUnitPrice * (1 + taxRate / 100) + Number.EPSILON) * 100) / 100;
