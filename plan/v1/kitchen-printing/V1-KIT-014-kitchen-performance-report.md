# V1-KIT-014 - Mutfak performans raporu (backend)

- Task ID: V1-KIT-014
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Rakip araştırması (`docs/engineering/kitchen-allday-view-and-performance-
report-research.md`, 2026-09-14) — Toast'ın "Tickets by Hour"/"Tickets by
Fulfillment Time", Lightspeed'in "KDS Statistics" raporlarının ALKAROS
karşılığı, ama araştırmanın kendi bulduğu gerçek bir tasarım dersiyle:
**ortalama yalnız başına yanıltıcı olabilir** (Fresh KDS'in kendi
dokümanı) — bu yüzden istasyon bazlı süre metriği hem ortalama HEM medyan
olarak döner. Veri kaynağı tamamen mevcut `kitchen.kitchen_tickets`
tablosunun zaten var olan zaman damgaları (`created_at`, `ready_at`,
`cancelled_at`) — yeni bir event-tracking şeması icat edilmez.

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs,
  KitchenOperationsStore.cs, KitchenOperationsContracts.cs (Sınırlı ek —
  V1-RMD-082 sahipliğinde kalan dosyalar) — yeni `GET
  /kitchen-operations/operations/performance-report` ucu; yeni bir
  `IKitchenTicketRepository` sorgu metodu (tarih aralığına göre
  tamamlanmış/iptal edilmiş biletleri okuyan).
- src/Modules/Kitchen/TicketLifecycle/** (Sınırlı ek — V1-KIT-001
  sahipliğinde kalan dosyalar) — `IKitchenTicketRepository`'ye yeni bir
  salt-okunur sorgu metodu.
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek) — yeni testler.

## In scope

1. `from`/`to` (UTC, ISO-8601) parametreli bir GET ucu; varsayılan aralık
   yoksa 400.
2. İstasyon bazlı: bilet sayısı, ortalama tamamlama süresi (dk),
   **medyan** tamamlama süresi (dk) — `created_at` → `ready_at` farkı,
   yalnız `Ready`/`Cancelled` OLMAYAN (yani gerçekten tamamlanmış)
   biletler.
3. Sabit `DefaultTargetPrepMinutes` (15dk) hedefini aşan biletlerin
   yüzdesi — istasyon bazlı. Dürüst etiketlenir: "hedef" ürün bazlı bir
   tahmin DEĞİL, sabit bir varsayılan (kod içi sabit, `KitchenTicket
   .DefaultTargetPrepMinutes`).
4. Saatlik bilet hacmi (o gün içindeki her saat için tamamlanan bilet
   sayısı).

## Out of scope

- "Düzeltme oranı" (undo/correction rate) — bugün undo anında hiçbir
  audit event yazılmıyor, bu veri henüz yok (araştırma dosyasının kendi
  bulgusu). Önce ayrı bir görevle `UndoItemAsync`'e audit-event yazma
  adımı eklenmesi gerekir.
- Ürün bazlı gerçek hazırlama-süresi tahmini (Lightspeed/Oracle'ın
  yaptığı) — bugün böyle bir veri modeli yok, icat edilmez.
- Frontend (`V1-KDS-009`).
- Çalışan bazlı performans (Foodics'in "staff productivity"si) —
  hassas bir alan, ayrı bir ürün kararı gerektirir, bu görevde yok.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek
  Postgres'e karşı yeşil; en az üç yeni test — bilinen zaman
  damgalarıyla seed edilmiş biletlerden doğru ortalama/medyan
  hesaplandığı, hedef aşımı yüzdesinin doğru sayıldığı, saatlik hacmin
  doğru gruplandığı (medyanın ortalamadan gerçekten farklı çıktığı bir
  senaryo — testin anlamlı olduğunu kanıtlamak için).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.

## Handoff

- V1-KDS-009
