export interface ChannelDayRow {
  businessDate: string; source: string; provider: string | null;
  ordersReceived: number; awaitingConfirmation: number; accepted: number; rejected: number; cancelled: number;
  acceptedValue: number; acceptedNetValue: number; cancelledValue: number; providerRefused: number;
}
export interface ChannelReport {
  from: string; to: string; days: readonly ChannelDayRow[];
  check: { isBalanced: boolean };
}
export type ChannelFilter = "" | "Qr" | "Online";

export const maxDays = 31;

const sources: Record<string, string> = { Qr: "QR", Online: "Online" };
const providers: Record<string, string> = { yemeksepeti: "Yemeksepeti", "trendyol-go": "Trendyol Go" };

export const sourceLabel = (source: string) => sources[source] ?? "Diğer";
export const providerLabel = (provider: string | null) => provider === null ? "—" : providers[provider] ?? "Diğer platform";

export const isoDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

export function dayCount(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000) + 1;
}

export const formatDay = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString("tr-TR");

export interface ChannelTotals {
  received: number; accepted: number; rejected: number; cancelled: number; refused: number; acceptedValue: number; acceptedNetValue: number;
}

export function totalsOf(days: readonly ChannelDayRow[]): ChannelTotals {
  return days.reduce<ChannelTotals>((sum, day) => ({
    received: sum.received + day.ordersReceived,
    accepted: sum.accepted + day.accepted,
    rejected: sum.rejected + day.rejected,
    cancelled: sum.cancelled + day.cancelled,
    refused: sum.refused + day.providerRefused,
    acceptedValue: sum.acceptedValue + day.acceptedValue,
    acceptedNetValue: sum.acceptedNetValue + day.acceptedNetValue,
  }), { received: 0, accepted: 0, rejected: 0, cancelled: 0, refused: 0, acceptedValue: 0, acceptedNetValue: 0 });
}
