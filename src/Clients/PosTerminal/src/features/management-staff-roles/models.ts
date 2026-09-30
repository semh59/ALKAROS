export interface RoleInfo { roleId: string; code: string; name: string; permissionCodes: readonly string[] }
export interface PermissionInfo { code: string; name: string }
export interface UserInfo { userId: string; username: string; displayName: string; active: boolean; roleIds: readonly string[] }

const permissionLabels: Record<string, string> = {
  "bills.comp": "Teslim edilen kalemi ikram etme",
  "bills.discount": "Kaleme veya hesaba indirim uygulama",
  "bills.split": "Hesap bölme",
  "bills.void": "Gönderilmiş kalemi iptal etme",
  "cash.drawer": "Para çekmecesi işlemleri ve sayım",
  "cash.session.override": "Farklı kasa oturumunu yönetici onayıyla kapatma",
  "catalog.manage": "Ürün kataloğunu yönetme",
  "floorplan.manage": "Salon, masa yerleşimi ve kapasite düzenleme",
  "identity.device_sessions.manage": "Cihaz oturumlarını yönetme",
  "identity.permissions.manage": "İzin kataloğunu yönetme",
  "identity.roles.manage": "Rolleri ve rol atamalarını yönetme",
  "identity.users.manage": "Personel hesaplarını yönetme",
  "integrations.manage": "Entegrasyon bilgilerini ayarlama",
  "inventory.manage": "Stok konumlarını ve kalemlerini yönetme",
  "kitchen.advance": "Mutfak fişini bir sonraki aşamaya ilerletme",
  "kitchen.availability.suspend": "Ürünü mutfaktan tükendi işaretleme",
  "kitchen.reprint": "Mutfak fişi yeniden yazdırmayı onaylama",
  "kitchen.routing.manage": "Mutfak yazıcı yönlendirmesini yönetme",
  "menu.manage": "Menüleri ve günün menüsünü yönetme",
  "observability.manage": "Uyarıları ve sağlık kontrollerini yönetme",
  "operations.backup": "Yedekleme işlemlerini başlatma ve izleme",
  "orders.create": "Gönderilmemiş sipariş oluşturma ve düzenleme",
  "orders.send": "Siparişi veya servisi mutfağa gönderme",
  "orders.transfer-server": "Kendi açık siparişlerini başka garsona devretme",
  "orders.transfer-server-any": "Herhangi bir garsonun açık siparişlerini devretme",
  "payments.take": "Hesaptan ödeme alma",
  "production.manage": "Üretim partilerini yönetme",
  "purchasing.manage": "Tedarikçi, satın alma siparişi ve mal kabul yönetimi",
  "reconciliation.manage": "Mutabakat farklarını inceleme ve çözme",
  "reports.close-day": "Gün açma ve gün sonu kapatma",
  "reports.view": "Raporları görüntüleme",
  "security.manage": "Hesap ve güvenlik işlemlerini yönetme",
  "settings.manage": "Sistem ayarlarını yönetme",
  "tables.merge": "Masaları birleştirme ve ayırma",
  "tables.reserve": "Rezervasyon oluşturma, iptal etme veya devralma",
  "tables.status": "Kendi masalarının durumunu değiştirme",
  "tables.transfer": "Açık sipariş veya hesabı masalar arasında taşıma",
};

const seededRoleNames: Record<string, string> = {
  cashier: "Kasiyer",
  "kitchen-chef": "Mutfak şefi",
  "kitchen-staff": "Mutfak personeli",
  manager: "Yönetici",
  supervisor: "Şef garson",
  waiter: "Garson",
};

export const permissionLabel = (code: string) => permissionLabels[code] ?? "Tanımsız izin";
export const roleLabel = (role: { code: string; name: string }) => seededRoleNames[role.code] ?? role.name;
