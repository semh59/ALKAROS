export interface SettingRecord {
  settingId: string; key: string; value: string; dataType: string; requiresRestart: boolean; active: boolean; updatedAt: string; rowVersion: number;
}
export interface SettingHistoryEntry {
  settingHistoryId: string; oldValue: string | null; newValue: string; reason: string | null; changedBy: string | null; changedByName?: string | null; changedAt: string;
}

const labels: Record<string, string> = {
  "business.accent_theme": "Renk teması",
  "business.name": "İşletme adı",
  "kitchen.dense_mode_threshold": "Mutfak yoğun mod eşiği",
  "kitchen.live_sync_enabled": "Mutfak canlı eşitleme",
  "waiter.max_active_tables": "Garson başına en fazla açık masa",
  "qr_ordering.pending_confirmation_timeout": "QR siparişi onay bekleme süresi",
  "reservations.dedicated_station_enabled": "Ayrı rezervasyon istasyonu",
  "garson.course_management_enabled": "Servis sırası (kurs) yönetimi",
  "garson.guest_live_bill_enabled": "Misafirin canlı hesap görünümü",
  "garson.help_request_enabled": "Garson yardım çağrısı",
  "garson.party_size_enabled": "Kişi sayısı sorulması",
  "garson.personal_comp_budget_enabled": "Kişisel ikram bütçesi",
  "garson.seat_assignment_enabled": "Koltuk ataması",
  "garson.shift_handoff_notes_enabled": "Vardiya devir notları",
  "garson.shift_summary_enabled": "Vardiya özeti",
  "garson.voluntary_tip_enabled": "İsteğe bağlı bahşiş",
};

export const settingLabel = (key: string) => labels[key] ?? "Tanımsız ayar";

/** How the screen lets a manager change a setting; the server enforces the same ranges. */
export type SettingEditRule =
  | { kind: "toggle" }
  | { kind: "number"; min: number; max: number; hint: string }
  | { kind: "minutes"; min: number; max: number; hint: string };

const numericRules: Record<string, SettingEditRule> = {
  "waiter.max_active_tables": { kind: "number", min: 0, max: 50, hint: "0 ile 50 arası; 0 = sınırsız" },
  "kitchen.dense_mode_threshold": { kind: "number", min: 1, max: 99, hint: "1 ile 99 arası" },
  "qr_ordering.pending_confirmation_timeout": { kind: "minutes", min: 1, max: 1440, hint: "1 dakika ile 24 saat (1440 dakika) arası" },
};

export function editRule(setting: Pick<SettingRecord, "key" | "dataType" | "active">): SettingEditRule | undefined {
  if (!setting.active) return undefined;
  return setting.dataType === "Toggle" ? { kind: "toggle" } : numericRules[setting.key];
}

/** "00:05:00" → 5, "1.00:00:00" → 1440. */
export function durationToMinutes(value: string): number {
  const [head, ...rest] = value.split(".");
  const [days, clock] = rest.length > 0 && !head.includes(":") ? [Number(head), rest.join(".")] : [0, value];
  const [hours, minutes] = clock.split(":").map(Number);
  return days * 1440 + hours * 60 + minutes;
}

export function minutesToDuration(minutes: number): string {
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const rest = minutes % 60;
  const clock = `${String(hours).padStart(2, "0")}:${String(rest).padStart(2, "0")}:00`;
  return days > 0 ? `${days}.${clock}` : clock;
}

function formatMinutes(minutes: number): string {
  if (minutes >= 60 && minutes % 60 === 0) return `${minutes / 60} saat`;
  return `${minutes} dakika`;
}

export function formatSettingValue(dataType: string, value: string | null, key?: string): string {
  if (value === null || value === "") return "—";
  if (value === "[Deactivated]") return "Pasifleştirildi";
  if (dataType === "Toggle") return value.toLowerCase() === "true" ? "Evet" : "Hayır";
  if (dataType === "Duration") return formatMinutes(durationToMinutes(value));
  if (key === "waiter.max_active_tables") return value === "0" ? "Sınırsız" : `${value} masa`;
  return value;
}

const whenFormat = new Intl.DateTimeFormat("tr-TR", { timeZone: "Europe/Istanbul", dateStyle: "short", timeStyle: "short" });
export const formatWhen = (iso: string) => whenFormat.format(new Date(iso));

const systemReasons: Record<string, string> = {
  "Initial registration": "İlk kayıt",
  "Setting deactivated": "Ayar pasifleştirildi",
};
export const formatReason = (reason: string | null) => reason ? systemReasons[reason] ?? reason : "—";
