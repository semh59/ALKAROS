export interface UserLookup { userId: string; displayName: string; active: boolean; isLocked: boolean }
export interface MaintenanceJob {
  name: string; description: string; intervalSeconds: number; enabled: boolean; disabledReason: string | null;
  lastStatus: string; lastRunAt: string | null; lastDurationMilliseconds: number | null; lastSummary: string | null;
}
export interface RpoStatus { dataClass: string; targetSeconds: number; measuredGapSeconds: number | null; meetsTarget: boolean }
export interface RestoreAttempt {
  artifactId: string; dataClass: string; startedAtUtc: string; durationSeconds: number; succeeded: boolean;
  withinRtoTarget: boolean; integrityChecksPassed: number; integrityChecksTotal: number; failureReason: string | null;
}
export interface DiagnosticBundle {
  bundleId: string; generatedAt: string; sizeBytes: number; logEntries: readonly unknown[]; systemStatus: { unhealthyCheckCount: number };
}
export interface OrderBacklog { liveOrders: number; provablySettled: number; withoutBill: number; withOpenBill: number; oldestSettledCreatedAt: string | null }
export interface CloseSettledResult { dryRun: boolean; eligible: number; closed: number; failed: number; sampleOrderNumbers: readonly string[] }

export interface Alert {
  alertId: string; alertType: string; severity: string; status: string; title: string; message: string; openedAt: string; rowVersion: number;
}
export interface HealthCheck { healthCheckId: string; checkType: string; target: string; status: string; checkedAt: string }
export type AlertAction = "acknowledge" | "escalate" | "suppress" | "resolve";

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const jobStatusLabel = label({ NeverRun: "Hiç çalışmadı", Succeeded: "Başarılı", Failed: "Başarısız" });
export const dataClassLabel = label({ Fiscal: "Mali kayıtlar", OrdersInventory: "Sipariş ve stok", Settings: "Ayarlar" });
export const alertSeverityLabel = label({ Info: "Bilgi", Warning: "Uyarı", Critical: "Kritik" });
export const alertStatusLabel = label({ Open: "Açık", Acknowledged: "Görüldü", Escalated: "Üst seviyeye taşındı", Suppressed: "Susturuldu", Resolved: "Çözüldü" });
export const healthStatusLabel = label({ Healthy: "Sağlıklı", Degraded: "Kısmen bozuk", Unhealthy: "Sağlıksız" });

export const formatWhen = (iso: string | null) => iso ? new Date(iso).toLocaleString("tr-TR") : "—";

export function formatDuration(seconds: number): string {
  if (seconds < 3600) return `${Math.round(seconds / 60)} dk`;
  if (seconds < 172_800) return `${Math.round(seconds / 3600)} sa`;
  return `${Math.round(seconds / 86_400)} gün`;
}

export const fill = (template: string, ...values: readonly (string | number)[]) =>
  values.reduce<string>((text, value, index) => text.replace(`{${index}}`, String(value)), template);
