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
  kitchen: "Mutfak",
  catalog: "Menü",
  system: "Sistem",
} as const;

/**
 * Cross-cutting shell actions. Per-workspace action copy (cancel / confirm /
 * save labels) is migrated in a follow-up pass.
 */
export const commonActions = {
  refresh: "Yenile",
  retry: "Tekrar dene",
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

export const catalogEntityLabels: Record<CatalogEntityKind, string> = {
  categories: "Kategoriler",
  taxes: "Vergi profilleri",
  products: "Ürünler",
  modifiers: "Modifikatörler",
  prices: "Fiyatlar",
};

export const catalogAddLabels: Record<CatalogEntityKind, string> = {
  products: "Ürün",
  categories: "Kategori",
  taxes: "Vergi profili",
  modifiers: "Modifier",
  prices: "Fiyat",
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
  Cleaning: "Temizlik",
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
