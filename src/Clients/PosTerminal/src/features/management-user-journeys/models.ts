export interface ActionSummary { key: string; count: number; averageMilliseconds: number; maxMilliseconds: number; errorCount: number }
export interface RequestSummary {
  traceId: string; method: string; route: string; statusCode: number; startedAt: string; durationMilliseconds: number;
  spanCount: number; failedSpanCount: number;
}
export interface JourneyStep { kind: string; name: string; offsetMilliseconds: number; durationMilliseconds: number; failed: boolean }
export interface Journey { traceId: string; startedAt: string | null; requestCaptured: boolean; droppedSpans: number; steps: readonly JourneyStep[] }
export interface StaffMovement { occurredAt: string; actorId: string | null; actorName: string | null; eventName: string; aggregateType: string }
export interface ActionFilter { sort: string; role: string; terminalId: string }

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const stepKindLabel = label({ interaction: "Düğme", request: "İstek", database: "Veritabanı", external: "Dış çağrı", event: "Olay" });
export const roleLabel = label({ cashier: "Kasiyer", waiter: "Garson", kitchen: "Mutfak", manager: "Müdür" });
export const sortLabel = label({ slowest: "En yavaş", failing: "En hatalı", recent: "En yeni" });

export const formatWhen = (iso: string) => new Date(iso).toLocaleString("tr-TR");
export const formatMilliseconds = (value: number) => `${Math.round(value).toLocaleString("tr-TR")} ms`;
export const isGuid = (value: string) => /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
