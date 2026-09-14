# V1-KIT-014 - Mutfak performans raporu (backend)

- Task ID: V1-KIT-014
- Status: Done
- Assignee: Claude Sonnet 5
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

1. `from`/`to` (UTC, ISO-8601) parametreli bir GET ucu; parametre
   eksik/geçersizse 400.
2. İstasyon bazlı: `ready_at IS NOT NULL` olan (yani gerçekten Ready'e
   ulaşmış) biletlerin sayısı, ortalama tamamlama süresi (dk, `created_at`
   → `ready_at` farkı), **medyan** tamamlama süresi (dk).
3. Sabit `DefaultTargetPrepMinutes` (15dk) hedefini aşan biletlerin
   yüzdesi — istasyon bazlı. Dürüst etiketlenir: "hedef" ürün bazlı bir
   tahmin DEĞİL, sabit bir varsayılan (kod içi sabit, `KitchenTicket
   .DefaultTargetPrepMinutes`).
4. Saatlik bilet hacmi (aralık içindeki her saat için Ready'e ulaşmış
   bilet sayısı).
5. Yetki kapısı: `ApplicationPermissions.ReportsView` (`reports.view`) —
   mevcut `/audit/aggregate`/`/audit/correlation` uçlarıyla AYNI desen
   (`RequirePermissionAsync`, `RequireReadAsync` DEĞİL — bu bir yönetim
   raporu, herhangi bir kasiyer oturumu değil). Bugün yalnız
   supervisor/manager bu izni taşıyor (`docs/domain/authorization-model.md`
   §3); Mutfak Şefi'nin bu izni taşıyıp taşımayacağı bu görevin kapsamı
   dışında, ayrı bir ürün kararı gerektirir.

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

- `dotnet build ALKAROS.slnx -c Debug` → **0 uyarı, 0 hata** (doğrulandı;
  yol boyunca bulunan gerçek bir engel: `IKitchenTicketRepository`'nin
  arabirim üyesinde `to` parametre adı CA1716'yı — VB.NET'in `To`
  anahtar sözcüğüyle çakışma — tetikledi, `windowStart`/`windowEnd`
  olarak yeniden adlandırılarak düzeltildi).
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek
  Postgres'e karşı **26/26 yeşil** (22 mevcut + 4 yeni: bilinen zaman
  damgalarıyla seed edilmiş 5/10/30dk'lık üç biletten doğru ortalama
  (15.0) VE medyan (10.0 — ortalamadan GERÇEKTEN farklı, testin anlamlı
  olduğunu kanıtlıyor) hesaplandığı, hedef aşımı yüzdesinin (1/3 =
  %33.33) doğru sayıldığı, saatlik hacmin iki farklı saate doğru
  gruplandığı, `reports.view` olmayan bir oturumun 403 aldığı,
  `from`/`to` eksikken 400 döndüğü).
- `dotnet test tests/Modules/Kitchen/TicketLifecycle` → **28/28 yeşil**,
  regresyon yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).

## Handoff

- V1-KDS-009
