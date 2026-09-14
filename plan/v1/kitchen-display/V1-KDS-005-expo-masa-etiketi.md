# V1-KDS-005 - Expo görünümünde gerçek masa etiketi

- Task ID: V1-KDS-005
- Status: Planned
- Assignee: Unassigned
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

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (yeni
  testler: masalı bir siparişte "Masa N" gösterimi, masasız bir siparişte
  kısaltılmış sipariş kimliğine geri düşme).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: bir masaya sipariş gönder,
  Mutfak ekranında kart başlığında gerçek masa numarasını gör; bir paket
  sipariş gönder, kart başlığında (masa yok) sipariş kimliğinin
  kısaltılmış halini gör.

## Handoff

- None
