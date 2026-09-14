# V1-KDS-008 - Mutfak ekranında Tüm Gün Görünümü

- Task ID: V1-KDS-008
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Rakip araştırması (`docs/engineering/kitchen-allday-view-and-performance-
report-research.md`, 2026-09-14) — Toast KDS'in "All Day View"'ının
ALKAROS karşılığı: Expo görünümünün yanında ikinci bir görünüm modu,
istasyondaki tüm açık kalemleri masa/sipariş bazında değil ÜRÜN bazında
toplar (ör. "6× Izgara Köfte"). Yüksek hacimli dar-menü istasyonlarında
elle sayma ihtiyacını kaldırır. Veri zaten Expo'nun kendi `data.tickets`'ında
mevcut — **yeni bir backend ucu gerekmez**, yalnız istemci tarafında
farklı bir toplama/görünüm. Toast'ın kendi gerçek hatasından ders:
**`Held` durumundaki kalemler toplamdan hariç tutulur** (bekletilen bir
kalemin "hazır" sanılmasını önlemek için).

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — yeni bir görünüm-modu
  toggle'ı (Expo ↔ Tüm Gün), ürün bazlı toplama bileşeni.

## In scope

1. `data.tickets`'taki tüm aktif (Cancelled olmayan) kalemleri ürün adına
   göre gruplayıp sayar.
2. `Held` durumundaki kalemler toplama dahil edilmez (bugün ALKAROS'ta
   `KitchenTicketItem.status` enum'unda `Held` diye bir değer yok —
   `Queued/Preparing/Ready/Served/Cancelled`; bu görev o değeri icat
   etmez, yalnız gelecekte eklenirse hariç tutma kuralını şimdiden
   doğru yere — filtre listesine — yazar, bugün fiilen hiçbir kalemi
   etkilemez).
3. Yalnız ürün bazlı toplama (modifier alt gruplaması yok — fast-follow).

## Out of scope

- Backend değişikliği — hiç yok, veri zaten yüklü.
- Modifier bazlı alt gruplama (Toast'ın ikinci modu).
- `V1-KIT-014`/`V1-KDS-009` (performans raporu) — ayrı bir görev.

## Dependencies

- V1-KDS-001

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (yeni
  testler: aynı üründen farklı biletlerde geçenlerin tek satırda
  toplandığı; iptal edilmiş kalemlerin toplama dahil edilmediği).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: aynı üründen birden fazla bilette
  sipariş gönder, Tüm Gün Görünümü'ne geçip tek satırda doğru toplamı
  gör.

## Handoff

- None
