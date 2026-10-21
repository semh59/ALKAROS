/**
 * Manager decision surface types (V1-IAM-020). The workspace shows the pending
 * authorization requests plus the active delegations and open behavioural
 * tightenings a manager can retire, and every action is a server call.
 */

export type AuthorizationDecisionState =
  | "loading"
  | "ready"
  | "error"
  | "offline"
  | "unauthorized";

export interface PendingGrant {
  grantId: string;
  permissionCode: string;
  requesterUserId: string;
  requesterRoleCode: string;
  amount: number;
  reasonCode: string;
  requestedAt: string;
  requesterName?: string;
}

export interface ActiveDelegation {
  delegationId: string;
  permissionCode: string;
  granteeUserId: string;
  limitAmount: number;
  expiresAt: string;
  granteeName?: string;
  delegatorName?: string;
}

export interface OpenTightening {
  tighteningId: string;
  userId: string;
  permissionCode: string;
  recentCount: number;
  triggerRatio: number;
  triggeredAt: string;
  userName?: string;
}

/** Turkish labels for the granular permission codes (UI_STYLE_GUIDE: no raw codes). */
export const permissionLabels: Record<string, string> = {
  "bills.void": "Adisyon iptali",
  "bills.comp": "İkram",
  "bills.discount": "İndirim",
  "bills.split": "Hesap bölme",
  "tables.reserve": "Masa rezervasyonu",
  "tables.transfer": "Masa taşıma",
  "tables.merge": "Masa birleştirme",
  "cash.drawer": "Kasa çekmecesi",
};

export function permissionLabel(code: string): string {
  return permissionLabels[code] ?? code;
}

/** Turkish labels for the void, complimentary and discount reason catalogs. */
export const reasonLabels: Record<string, string> = {
  OperatorError: "Operatör hatası",
  ProductUnavailable: "Ürün tükendi",
  CustomerChange: "Müşteri talebi",
  DuplicateEntry: "Çift giriş",
  CustomerSatisfaction: "Müşteri memnuniyeti",
  ManagerPromotion: "Yönetici ikramı",
  VIPGuest: "Özel misafir",
  ServiceApology: "Servis özrü",
  CustomerLoyalty: "Müşteri sadakati",
  PromotionalOffer: "Kampanya",
  ServiceRecovery: "Servis telafisi",
  ManagerDiscretion: "Yönetici takdiri",
};

export function reasonLabel(code: string): string {
  return reasonLabels[code] ?? code;
}

const roleNames: Record<string, string> = {
  cashier: "Kasiyer",
  manager: "Yönetici",
  supervisor: "Şef garson",
  waiter: "Garson",
};

export function roleName(code: string): string {
  return roleNames[code] ?? code;
}

const istanbulTime = new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", timeZone: "Europe/Istanbul" });

export function formatTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : istanbulTime.format(date);
}

export const authorizationDecisionText = {
  kicker: "YÖNETİM / YETKİ",
  title: "Yetki kararları",
  subtitle: "Bekleyen istekler, süreli devirler ve davranışsal sıkılaştırmalar",
  pendingHeading: "Bekleyen yetki istekleri",
  pendingEmpty: "Bekleyen yetki isteği yok.",
  delegationHeading: "Etkin süreli devirler",
  delegationEmpty: "Etkin devir yok.",
  tighteningHeading: "Açık davranışsal sıkılaştırmalar",
  tighteningEmpty: "Açık sıkılaştırma yok.",
  approve: "Onayla",
  deny: "Reddet",
  revoke: "Devri geri al",
  clear: "Sıkılaştırmayı kaldır",
  loadingTitle: "Yetki kararları yükleniyor",
  loadingBody: "Bekleyen istekler ve devirler alınıyor…",
  errorTitle: "Yetki verileri alınamadı",
  offlineBody: "Güncel yetki verisi alınamıyor.",
  actionFailed: "İşlem tamamlanamadı. Listeyi yenileyip tekrar deneyin.",
  requestedBy: "İsteyen",
  unknownPerson: "Bilinmeyen personel",
  delegatedTo: "Devralan",
  delegatedBy: "Veren",
  requestedAt: "İstek",
  until: "Bitiş",
  since: "Son güncelleme",
  unauthorizedBody: "Yetki kararları yalnızca yönetici ve şef garson rolüne açıktır.",
} as const;
