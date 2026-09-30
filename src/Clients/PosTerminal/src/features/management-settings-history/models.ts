export interface SettingRecord {
  settingId: string; key: string; value: string; dataType: string; requiresRestart: boolean; active: boolean; updatedAt: string; rowVersion: number;
}
export interface SettingHistoryEntry {
  settingHistoryId: string; oldValue: string | null; newValue: string; reason: string | null; changedBy: string | null; changedAt: string;
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

export function formatSettingValue(dataType: string, value: string | null): string {
  if (value === null || value === "") return "—";
  if (dataType === "Toggle") return value.toLowerCase() === "true" ? "Evet" : "Hayır";
  return value;
}

export const formatWhen = (iso: string) => new Date(iso).toLocaleString("tr-TR");

const systemReasons: Record<string, string> = { "Initial registration": "İlk kayıt" };
export const formatReason = (reason: string | null) => reason ? systemReasons[reason] ?? reason : "—";
