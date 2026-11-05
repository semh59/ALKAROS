# V1-RMD-827 - Tek İstanbul iş günü kuralı: mutfak raporu, kanal/fatura/marj raporları ve varsayılan iş günü tarihleri

- Task ID: V1-RMD-827
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Sunucu iş günü 06:00 Europe/Istanbul - ertesi gün 06:00 (`BusinessDayWindow`, V1-RMD-790/791). İstemcide üç ayrı kural vardı: mutfak "bugün" raporu UTC gece yarısından, kanal/fatura/marj raporlarının varsayılan aralığı cihaz yerel gününden, gün sonu/stok/menü/reçete maliyeti tarihleri takvim gününden (V1-RMD-823) başlıyordu. Gece 00:00-06:00 arası bu ekranlar farklı günleri gösterir. Düzeltme: `format.ts` içinde tek kural (`businessDate`, `businessDayStart`, `businessDateRange`; Türkiye'de yaz saati yok, sabit +03:00) ve beş yerin hepsi onu kullanır; testler sınırları (02:59:59Z / 03:00Z) kilitler.

## Owned surface

- `evidence/V1-RMD-827/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/format.ts (V1-RMD-784) — iş günü yardımcıları, `venueToday` yerine.
  - src/Clients/PosTerminal/src/format.test.ts (V1-RMD-784) — sınır testleri.
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx (V1-RMD-567) — rapor penceresi.
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx (V1-RMD-567) — pencere testi.
  - src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.tsx (V1-RMD-466) — varsayılan aralık.
  - src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.test.tsx (V1-RMD-466) — aralık testi.
  - src/Clients/PosTerminal/src/features/management-channel-report/models.ts (V1-RMD-466) — yerel `isoDate` kalkar.
  - src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.tsx (V1-RMD-473) — varsayılan aralık.
  - src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.test.tsx (V1-RMD-473) — aralık testi.
  - src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.tsx (V1-RMD-488) — varsayılan aralık.
  - src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.test.tsx (V1-RMD-488) — aralık testi.
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx (V1-RMD-792) — varsayılan iş günü.
  - src/Clients/PosTerminal/src/features/management-stock/StockSection.tsx (V1-RMD-567) — varsayılan iş günü.
  - src/Clients/PosTerminal/src/features/management-menus-recipe-cost/MenusSection.tsx (V1-RMD-567) — varsayılan iş günü.
  - src/Clients/PosTerminal/src/features/management-menus-recipe-cost/RecipeCostSection.tsx (V1-RMD-567) — varsayılan iş günü.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları.

## Out of scope

- Sunucu iş günü kuralı (V1-RMD-790/791); vanilla kasa ekranları.

## Dependencies

- V1-RMD-826

## Acceptance evidence

1. PosTerminal vitest tam paket yeşil, tsc ve oxlint temiz; kırmızı sonra yeşil: iş günü 06:00 İstanbul'da döner, mutfak penceresi, üç rapor aralığı ve dört varsayılan tarih aynı kuralı kullanır.
2. Mutasyon: kural bozulunca ilgili test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi, bulgular giderilir, doğrulama turu.
5. Semih'in deneyeceği senaryo: gece 02:00'de mutfak raporu, kanal raporu ve gün sonu aynı (önceki) iş gününü gösterir.

## Handoff

- None
