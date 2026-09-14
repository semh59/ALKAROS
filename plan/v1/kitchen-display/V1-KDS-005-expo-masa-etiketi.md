# V1-KDS-005 - Expo görünümünde gerçek masa etiketi

- Task ID: V1-KDS-005
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-KIT-012`'nin ürettiği `TableId`/`TableNumber` alanlarını Expo
görünümüne bağlar. `groupByOrder` (sipariş kimliğiyle gruplama, kayıtlı
kapsam sapması) kaldırılmaz — hâlâ birden fazla istasyona düşen bir
siparişin biletlerini bir arada tutan doğru mekanizma — ama kart
başlığında artık kısaltılmış sipariş kimliği yerine gerçek masa numarası
gösterilir (`"Sipariş {id}"` yerine `"Masa {tableNumber}"`); masasız
(paket/bar) siparişler için sipariş kimliği geri düşer (fallback), veri
icat edilmez.

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — `OrderGroupCard` başlığı,
  `models.ts`'teki `KitchenTicket` arayüzüne `tableId`/`tableNumber`.

## Out of scope

- `V1-KIT-012`'nin kendisi.
- Gerçek masa numarasına göre gruplama (aynı masanın farklı siparişlerini
  birleştirme) — bu ayrı bir kapsam kararı, bu görev yalnız ETİKETİ
  değiştirir, gruplama anahtarını değiştirmez.

## Dependencies

- V1-KIT-012

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata** (doğrulandı).
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 155 passed (155)** — tüm proje, izole değil (2 yeni test:
  masalı bir siparişte kart başlığının "Masa"/gerçek numara gösterdiği;
  masasız bir siparişte (paylaşılan test fixture'ının kendi `tableId:
  null, tableNumber: null`'ı) "Sipariş"/kısaltılmış sipariş kimliğine
  geri düştüğü).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Uygulanan tasarım: `OrderGroupCard`, grubun ilk biletinin
  `tableNumber`'ını kullanır (aynı `orderId`'ye sahip her bilet aynı
  masayı paylaşır); doluysa etiket "Masa"/gerçek numara, boşsa (paket/bar)
  "Sipariş"/kısaltılmış sipariş kimliği — gruplama ANAHTARI hâlâ
  `orderId` (Out of scope'ta belirtildiği gibi, bu görev yalnız ETİKETİ
  değiştirdi).
- Semih'in elle deneyebileceği senaryo: bir masaya sipariş gönder,
  Mutfak ekranında kart başlığında gerçek masa numarasını gör; bir paket
  sipariş gönder, kart başlığında (masa yok) sipariş kimliğinin
  kısaltılmış halini gör.

## Handoff

- None
