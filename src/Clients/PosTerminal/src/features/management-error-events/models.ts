export interface ErrorEventItem {
  fingerprint: string; layer: string; severity: string; category: string; status: string;
  occurrenceCount: number; firstSeenAt: string; lastSeenAt: string; terminalLabel: string | null; synthetic: boolean;
}
export interface ErrorOccurrence { occurredAt: string; severity: string; occurrences: number }
export interface ErrorEventDetail { event: ErrorEventItem; recent: readonly ErrorOccurrence[] }
export interface ErrorEventPage { items: readonly ErrorEventItem[]; total: number; page: number; pageSize: number }
export interface ErrorFilter { layer: string; severity: string; status: string }

const label = (map: Record<string, string>) => (value: string) => map[value] ?? "Bilinmiyor";
export const layerLabel = label({
  Database: "Veritabanı", Api: "Sunucu", Background: "Arka plan işi", Printer: "Yazıcı", Payment: "Ödeme",
  Kitchen: "Mutfak", Integration: "Entegrasyon", Browser: "Tarayıcı", Security: "Güvenlik", Backup: "Yedekleme",
});
export const severityLabel = label({ Info: "Bilgi", Warning: "Uyarı", Error: "Hata", Critical: "Kritik" });
export const statusLabel = label({ Open: "Açık", Acknowledged: "Görüldü", Resolved: "Çözüldü" });
export const categoryLabel = label({
  "api.unhandled_exception": "Beklenmeyen sunucu hatası", "api.slow_request": "Yavaş istek",
  "db.connection_failure": "Veritabanına bağlanılamadı", "db.cannot_connect_now": "Veritabanı şu an bağlantı kabul etmiyor",
  "db.too_many_connections": "Çok fazla veritabanı bağlantısı", "db.connection_saturation": "Bağlantı havuzu doldu",
  "db.deadlock_detected": "Veritabanı kilitlenmesi", "db.lock_wait": "Kilit bekleme", "db.lock_not_available": "Kilit alınamadı",
  "db.slow_statement": "Yavaş sorgu", "db.long_running_query": "Uzun süren sorgu", "db.table_bloat": "Tablo şişmesi",
  "printer.offline": "Yazıcı çevrimdışı", "security.login_bruteforce": "Çok sayıda hatalı giriş", "security.denial_burst": "Ani yetki reddi artışı",
  "browser.js_error": "Ekran hatası", "browser.unhandled_rejection": "Yakalanmamış işlem hatası",
  "browser.network_error": "Tarayıcı ağ hatası", "browser.render_error": "Ekran çizim hatası", "browser.unknown": "Bilinmeyen ekran hatası",
  "browser.offline_queue_backlog": "Çevrimdışı kuyruk birikti", "api.server_error_response": "Sunucu hata yanıtı verdi",
  "db.slow_query": "Yavaş sorgu", "db.slow_query_plan": "Yavaş sorgu planı", "db.n_plus_one": "Tekrarlayan sorgu", "db.pool_saturation": "Bağlantı havuzu doldu",
  "db.statement": "Sorgu ayrıntısı", "background.job_missed": "Arka plan işi çalışmadı",
  "backup.failed": "Yedekleme başarısız", "backup.restore_failed": "Geri yükleme başarısız", "backup.rpo_exceeded": "Yedek sıklığı aşıldı", "backup.stale": "Yedek eski",
  "integration.external_call_failed": "Dış servis çağrısı başarısız", "integration.external_call_slow": "Dış servis çağrısı yavaş",
  "integration.catalog_undelivered": "Menü platforma iletilemedi", "integration.provider_poll_failing": "Platform siparişleri alınamıyor",
  "integration.qr_tunnel_down": "QR bağlantı tüneli kapalı", "integration.store_status_undelivered": "Restoran durumu platforma iletilemedi",
  "kitchen.ticket_not_accepted": "Mutfak fişi kabul edilmedi", "payment.reconciliation_required": "Ödeme mutabakatı gerekiyor", "payment.unknown_outcome": "Ödeme sonucu belirsiz",
  "printer.delivery_unknown": "Yazdırma sonucu belirsiz", "printer.job_failed": "Yazdırma başarısız", "printer.queue_stuck": "Yazdırma kuyruğu takıldı",
});

export const formatWhen = (iso: string) => new Date(iso).toLocaleString("tr-TR");
export const fill = (template: string, ...values: readonly (string | number)[]) =>
  values.reduce<string>((text, value, index) => text.replace(`{${index}}`, String(value)), template);
