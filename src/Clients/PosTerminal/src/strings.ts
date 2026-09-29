/**
 * Central user-facing text catalog for the PosTerminal client.
 *
 * UI_STYLE_GUIDE requires every user-facing string to be Turkish and every enum
 * value to pass through a translation map. This module is the single source for
 * both, so untranslated leaks (the "Manager" case) are caught by inspecting one
 * file instead of a scanning heuristic (deep-analysis finding F-7).
 *
 * Migration is incremental: the cross-cutting text (roles, connectivity, common
 * actions, navigation) and every enum label map now live here. Feature-local
 * one-off copy is still being moved in - new strings belong here, not inline.
 */

import type { SplitMode } from "./features/billing/models";
import type { CatalogEntityKind } from "./features/catalog/models";
import type {
  KitchenBackup,
  KitchenHealthSnapshot,
  KitchenTicket,
  KitchenTicketItem,
} from "./features/kitchen-operations/models";
import type { TableAction, TableStatus } from "./features/tables/models";

/** Role labels shown in the shell. Derived from session capabilities, never from the route. */
export const roleLabels = {
  manager: "Yönetici",
  cashierOps: "Kasiyer / Operasyon",
  limited: "Sınırlı erişim",
} as const;

/** Connectivity status labels for the shell status bar. */
export const connectivityLabels = {
  online: "Çevrimiçi",
  reconnecting: "Yeniden bağlanıyor",
  offline: "Çevrimdışı",
} as const;

/** Primary navigation labels. */
export const navLabels = {
  sales: "Kasa",
  tables: "Masalar",
  billing: "Hesap",
  pendingChecks: "Bekleyen hesaplar",
  onlineFood: "Online Yemek",
  kitchen: "Mutfak",
  catalog: "Menü",
  system: "Sistem",
  authorization: "Yetki",
  management: "Yönetim",
} as const;

/** Yönetim alanı kabuğu ve Gün sonu ve mutabakat bölümü (V1-RMD-445). */
export const managementText = {
  title: "Yönetim",
  description: "Gün sonu, mutabakat ve işletme yönetimi bölümleri",
  sectionsLabel: "Yönetim bölümleri",
  noSectionTitle: "Bu oturumda açılabilir bölüm yok",
  noSectionBody: "Yönetim bölümleri için gerekli yetkilere sahip değilsiniz.",
  closing: {
    tab: "Gün sonu ve mutabakat",
    dayHeading: "İş günü",
    dateLabel: "Tarih",
    load: "Getir",
    noDay: "Bu tarih için iş günü kaydı yok.",
    openDay: "İş gününü aç",
    closeDay: "İş gününü kapat",
    closeHint: "Gelir ve sipariş sayısı sunucu tarafından kayıtlardan hesaplanır. İptal ve yazdırma hatası sayısını biliyorsanız girin; bilmiyorsanız 0 bırakın.",
    cancelledItems: "İptal edilen kalem sayısı",
    printFailures: "Yazdırma hatası sayısı",
    status: "Durum",
    openedAt: "Açılış",
    closedAt: "Kapanış",
    revenue: "Toplam ciro",
    orders: "Sipariş sayısı",
    cancelled: "İptal edilen kalem",
    printFailed: "Yazdırma hatası",
    waiters: "Garson performansı",
    waiter: "Garson",
    served: "Servis edilen sipariş",
    sales: "Satış tutarı",
    cancellations: "İptal",
    discounts: "İndirim",
    settlementHeading: "Ödeme mutabakat raporu",
    paymentMix: "Ödeme yöntemleri",
    method: "Yöntem",
    count: "Adet",
    amount: "Tutar",
    unsettled: "Sonuçlanmamış ödemeler",
    unknown: "Sonucu bilinmeyen",
    reconciliationRequired: "Mutabakat gereken",
    cashSessions: "Kasa oturumları",
    expected: "Beklenen nakit",
    actual: "Sayılan nakit",
    difference: "Fark",
    sessionOpen: "Açık",
    sessionClosed: "Kapalı",
    caseTotals: "Vaka özeti",
    caseType: "Vaka türü",
    openCases: "Açık",
    resolvedCases: "Çözülen",
    openDiscrepancy: "Açık fark",
    unavailableSections: "Bu bölümler henüz kullanılamıyor: iade net tutarı, mali durum, yemek kartı kapanışı.",
    manualHeading: "Elle onaylanan kart ödemeleri",
    slip: "Slip no",
    requestedAt: "Talep",
    decidedAt: "Karar",
    casesHeading: "Mutabakat vakaları",
    caseStatusFilter: "Vaka durumu",
    scan: "Ödeme taraması yap",
    scanDone: "Tarama tamamlandı; vaka listesi yenilendi.",
    severity: "Önem",
    opened: "Açılış",
    caseNote: "Karar notu (isteğe bağlı)",
    actions: "İşlem",
    empty: "Kayıt yok.",
    loading: "Yükleniyor…",
    loadFailed: "Veri alınamadı.",
    actionFailed: "İşlem tamamlanamadı.",
    dayOpened: "İş günü açıldı.",
    dayClosed: "İş günü kapatıldı.",
    caseUpdated: "Vaka güncellendi.",
  },
  stock: {
    tab: "Stok",
    criticalHeading: "Kritik stok",
    varianceHeading: "Gerçek ve teorik kullanım farkı",
    itemsHeading: "Stok kalemleri",
    locationsHeading: "Stok konumları",
    from: "Başlangıç",
    to: "Bitiş",
    run: "Raporu getir",
    excluded: "Açılış veya kapanış sayımı eksik olduğu için dışarıda kalan kalem sayısı",
    code: "Kod",
    name: "Ad",
    type: "Tür",
    unit: "Birim",
    reorderPoint: "Yeniden sipariş noktası",
    location: "Konum",
    onHand: "Eldeki miktar",
    available: "Kullanılabilir",
    threshold: "Eşik",
    status: "Durum",
    actionsColumn: "İşlem",
    critical: "Kritik",
    ok: "Yeterli",
    actual: "Gerçek kullanım",
    theoretical: "Teorik kullanım",
    variance: "Fark",
    variancePercent: "Fark %",
    active: "Aktif",
    passive: "Pasif",
    defaultLocation: "Varsayılan konum",
    none: "Yok",
    newItem: "Yeni stok kalemi",
    newLocation: "Yeni stok konumu",
    add: "Ekle",
    itemAdded: "Stok kalemi eklendi.",
    locationAdded: "Stok konumu eklendi.",
    count: "Sayım gir",
    waste: "Fire kaydet",
    setReorder: "Sipariş noktası",
    countedQuantity: "Sayılan miktar",
    notes: "Not (isteğe bağlı)",
    wasteSource: "Fire nedeni türü",
    quantity: "Miktar",
    reason: "Açıklama",
    reorderHint: "Boş bırakırsanız eşik kaldırılır.",
    save: "Kaydet",
    cancel: "Vazgeç",
    countSaved: "Sayım kaydedildi.",
    wasteSaved: "Fire kaydedildi.",
    reorderSaved: "Yeniden sipariş noktası kaydedildi.",
    invalidNumber: "Geçerli bir sayı girin.",
    pickLocation: "Bir konum seçin.",
    noLocations: "Önce bir stok konumu ekleyin.",
    empty: "Kayıt yok.",
    loading: "Yükleniyor…",
    loadFailed: "Veri alınamadı.",
    actionFailed: "İşlem tamamlanamadı.",
  },
} as const;

/**
 * Cross-cutting actions. Per-workspace form action copy (cancel / confirm /
 * save labels) is migrated in a follow-up pass.
 */
export const commonActions = {
  refresh: "Yenile",
  retry: "Tekrar dene",
  reload: "Yeniden yükle",
} as const;

/**
 * Shared workspace state chrome. Every workspace renders the same offline /
 * unauthorized / error scaffolding; the titles and the generic fallbacks live
 * here so the wording stays identical (finding F-7).
 */
export const stateText = {
  offlineTitle: "Bağlantı yok",
  unauthorizedTitle: "Oturum gerekli",
  managerUnauthorizedTitle: "Yönetici oturumu gerekli",
  unexpectedError: "Beklenmeyen bir hata oluştu.",
  dismissMessage: "Mesajı kapat",
} as const;

/** Kitchen reprint decision flow. */
export const kitchenReprintText = {
  reasonRequired: "Süpervizör gerekçesi zorunlu.",
  reasonLabel: "Süpervizör gerekçesi",
} as const;

// --- Enum translation maps (re-exported from each feature's models.ts) ---

export const modeLabels: Record<SplitMode, string> = {
  EqualByPerson: "Eşit böl",
  ByItem: "Ürün / miktar",
  ByAmount: "Tutar gir",
};

/**
 * Bill lifecycle status labels. `billStatus` arrives from the server as a
 * plain string (ALKAROS.Billing.BillFoundation.BillState.ToString()), not a
 * narrow union, so this is a lookup with a neutral fallback (UI_STYLE_GUIDE
 * §3) rather than an exhaustive Record.
 */
export const billStatusLabels: Record<string, string> = {
  Open: "Açık",
  PartiallyAllocated: "Kısmen paylaştırıldı",
  Allocated: "Paylaştırıldı",
  PartiallyPaid: "Kısmen ödendi",
  Paid: "Ödendi",
  Cancelled: "İptal",
  Reopened: "Yeniden açıldı",
};

export const catalogEntityLabels: Record<CatalogEntityKind, string> = {
  categories: "Kategoriler",
  taxes: "Vergi profilleri",
  products: "Ürünler",
  modifierGroups: "Modifikatör grupları",
  modifiers: "Modifikatörler",
  prices: "Fiyatlar",
};

export const catalogAddLabels: Record<CatalogEntityKind, string> = {
  products: "Ürün",
  categories: "Kategori",
  taxes: "Vergi profili",
  modifierGroups: "Modifikatör grubu",
  modifiers: "Modifikatör",
  prices: "Fiyat",
};

/**
 * Found by an independent audit (2026-09-06): CatalogWorkspace.tsx's product
 * type <option> labels and two section headers were left untranslated (a
 * modifier-assignments heading and an effective-price-timeline heading) --
 * the <option value="..."> stays the wire enum (correctly English), but the
 * visible text must not.
 */
export const productTypeLabels: Record<string, string> = {
  MenuItem: "Menü ürünü",
  Modifier: "Modifikatör",
  AddOn: "Ek ürün",
  Packaging: "Ambalaj",
  ServiceItem: "Hizmet kalemi",
};

/**
 * V1-RMD-437 (V1-RMD-398 G-11): the sale path never reads the stock mode - an order is only accepted when the product
 * has a stock mapping (V1-RMD-143) - so the form no longer offers "Untracked"; older records still carry it and show
 * as a legacy value.
 */
export const stockModeLabels: Record<string, string> = {
  Untracked: "Takipsiz (eski kayıt)",
  QuantityTracked: "Miktar takipli",
  PortionTracked: "Porsiyon takipli",
  RecipeDerived: "Reçeteden",
};

/** V1-RMD-438: the price row subtitle showed the raw PriceType enum. */
export const priceTypeLabels: Record<string, string> = {
  SalePrice: "Satış fiyatı",
};

export const selectableStockModes = ["QuantityTracked", "PortionTracked", "RecipeDerived"] as const;

export const stockModeHint = "Siparişin onaylanması için ürünün stok eşlemesi olmalı.";

export const selectionTypeLabels: Record<string, string> = {
  SelectOne: "Tek seçim",
  SelectMany: "Çoklu seçim",
};

export const ticketStatusLabels: Record<KitchenTicket["status"], string> = {
  Queued: "Bekliyor",
  Accepted: "Kabul edildi",
  Preparing: "Hazırlanıyor",
  Ready: "Hazır",
  Cancelled: "İptal",
};

export const itemStatusLabels: Record<KitchenTicketItem["status"], string> = {
  Queued: "Bekliyor",
  Preparing: "Hazırlanıyor",
  Ready: "Hazır",
  Served: "Servis edildi",
  Cancelled: "İptal",
};

export const healthStatusLabels: Record<KitchenHealthSnapshot["databaseStatus"], string> = {
  Healthy: "Sağlıklı",
  Degraded: "Sınırlı",
  Unhealthy: "Sorunlu",
};

export const backupStatusLabels: Record<KitchenBackup["status"], string> = {
  InProgress: "Sürüyor",
  Completed: "Tamamlandı",
  Failed: "Başarısız",
};

export const tableStatusLabels: Record<TableStatus, string> = {
  Available: "Müsait",
  Occupied: "Dolu",
  Reserved: "Rezerve",
  // V1-TBL-010 (Semih's catch, 2026-09-12): the previous label read as a
  // hard "don't touch yet" while the actual system behaviour is the
  // opposite for the common case — a check sent to the cashier frees the
  // table for a new party IMMEDIATELY (SendCheckToCashierAsync,
  // V1-ORD-006), no staff action or confirmation required. The new label
  // reads as informational (recently vacated, might not be tidied yet)
  // rather than prohibitive.
  Cleaning: "Toplanıyor",
  OutOfService: "Servis dışı",
};

export const tableActionLabels: Partial<Record<TableAction, string>> = {
  SetOccupied: "Masayı aç",
  SetAvailable: "Müsait yap",
  Reserve: "Rezervasyon al",
  CancelReservation: "Rezervasyonu iptal et",
  ClaimReservation: "Rezervasyonu sahiplen",
  Transfer: "Masa değiştir",
  Merge: "Masaları birleştir",
  Unmerge: "Birleşimi ayır",
  SetCleaning: "Temizliğe al",
  SetOutOfService: "Servis dışı yap",
};
