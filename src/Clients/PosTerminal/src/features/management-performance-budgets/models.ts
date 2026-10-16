export interface PerformanceBudget { metric: string; name: string; p95Ms: number }
export interface PerformanceBudgetList { defaultRequestP95Ms: number; items: readonly PerformanceBudget[] }

export const metrics = ["request", "db_command", "queue_depth", "ui_interaction"] as const;
export const metricLabel = (metric: string) => ({
  request: "İstek", db_command: "Veritabanı komutu", queue_depth: "Kuyruk derinliği", ui_interaction: "Ekran etkileşimi",
} as Record<string, string>)[metric] ?? "Bilinmiyor";

export const maxNameLength = 200;
export const maxP95 = 3_600_000;

/** Same limits as the server; the server stays the authority. */
export const isValidBudget = (name: string, p95Ms: number) =>
  name.trim().length > 0 && name.length <= maxNameLength && Number.isFinite(p95Ms) && p95Ms > 0 && p95Ms <= maxP95;
