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
