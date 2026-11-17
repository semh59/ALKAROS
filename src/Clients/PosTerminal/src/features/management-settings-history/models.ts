export interface SettingRecord {
  settingId: string; key: string; value: string; dataType: string; requiresRestart: boolean; active: boolean; updatedAt: string; rowVersion: number;
}
export interface SettingHistoryEntry {
  settingHistoryId: string; oldValue: string | null; newValue: string; reason: string | null; changedBy: string | null; changedByName?: string | null; changedAt: string;
}

const labels: Record<string, string> = {
  "approvals.manager_self_approval_enabled": "Yönetici kendi talebini ve kendi kasa mutabakatını onaylayabilir",
  "billing.adjustment_total_cap": "Hesap başına en fazla ikram ve indirim toplamı",
  "business.accent_theme": "Renk teması",
  "business.name": "İşletme adı",
  "cash.cash_out_approval_threshold": "Nakit çıkış onay eşiği",
  "cash.close_variance_tolerance": "Kasa kapanış fark toleransı",
  "cash.drawer_kick_enabled": "Kasa çekmecesini yazıcıdan aç",
  "cash.drawer_printer_address": "Kasa yazıcısı adresi",
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

const descriptions: Record<string, string> = {
  "cash.drawer_kick_enabled": "Nakit ödeme alınınca ve kasiyer \"Satış yok\" dediğinde çekmece yazıcı üzerinden açılır. Gerçek yazıcı ve çekmecede denemeden açmayın; varsayılan kapalıdır.",
  "kitchen.live_sync_enabled": "Mutfak ekranı (KDS) kullanıyorsanız açın. Açıkken kalemin durumu (Hazırlanıyor, Hazır, Servis edildi) siparişe yansır ve garsona hazır bildirimi gider. Hazır biletler servis edilene kadar panoda ayrı bölümde durur. Kapalıyken bu bölüm kalkar ve sipariş durumu değişmez.",
};

export const settingDescription = (key: string): string | undefined => descriptions[key];

export const settingLabel = (key: string) => labels[key] ?? "Tanımsız ayar";

/** How the screen lets a manager change a setting; the server enforces the same ranges. */
export type SettingEditRule =
  | { kind: "toggle" }
  | { kind: "number"; min: number; max: number; hint: string }
  | { kind: "minutes"; min: number; max: number; hint: string }
  | { kind: "text"; hint: string; isValid: (value: string) => boolean };

/** host:port with no spaces; the same rule the server applies. An empty value means "no printer". */
export function isPrinterAddress(value: string): boolean {
  if (value === "") return true;
  const parts = value.split(":");
  if (parts.length !== 2) return false;
  const [host, port] = parts;
  if (host.length === 0 || host.length > 253 || !/^[A-Za-z0-9.-]+$/.test(host)) return false;
  if (host.split(".").some((label) => label === "" || label.startsWith("-") || label.endsWith("-"))) return false;
  return /^\d+$/.test(port) && Number(port) >= 1 && Number(port) <= 65535;
}

const textRules: Record<string, Extract<SettingEditRule, { kind: "text" }>> = {
  "cash.drawer_printer_address": { kind: "text", hint: "Kasa yazıcısının adresi ve portu, örn. 192.168.1.50:9100; boş bırakırsanız çekmece açılmaz", isValid: isPrinterAddress },
};

const numericRules: Record<string, Extract<SettingEditRule, { kind: "number" | "minutes" }>> = {
  "waiter.max_active_tables": { kind: "number", min: 0, max: 50, hint: "0 ile 50 arası; 0 = sınırsız" },
  "kitchen.dense_mode_threshold": { kind: "number", min: 1, max: 99, hint: "1 ile 99 arası" },
  "cash.cash_out_approval_threshold": { kind: "number", min: 0, max: 1000000, hint: "0 ile 1.000.000 ₺ arası; bu tutarın üstündeki nakit çıkışlar yönetici onayı ister" },
  "cash.close_variance_tolerance": { kind: "number", min: 0, max: 500, hint: "0 ile 500 ₺ arası; kasa kapanışında bu tutarı aşan fark yönetici onayı ister" },
  "billing.adjustment_total_cap": { kind: "number", min: 0, max: 100000, hint: "0 ile 100.000 ₺ arası; bir hesapta kasiyer ve garsonun verdiği ikram ve indirim toplamı bu tutarı aşarsa yönetici onayı gerekir (0 = hepsi onay ister)" },
  "qr_ordering.pending_confirmation_timeout": { kind: "minutes", min: 1, max: 1440, hint: "1 dakika ile 24 saat (1440 dakika) arası" },
};

export function editRule(setting: Pick<SettingRecord, "key" | "dataType" | "active">): SettingEditRule | undefined {
  if (!setting.active) return undefined;
  return setting.dataType === "Toggle" ? { kind: "toggle" } : numericRules[setting.key] ?? textRules[setting.key];
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
  if (key === "cash.cash_out_approval_threshold" || key === "cash.close_variance_tolerance" || key === "billing.adjustment_total_cap") return `${value} ₺`;
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
