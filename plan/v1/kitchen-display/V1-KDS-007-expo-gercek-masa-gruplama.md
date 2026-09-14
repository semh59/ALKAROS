# V1-KDS-007 - Expo görünümünü gerçek masaya göre gruplama

- Task ID: V1-KDS-007
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-KDS-005`'in bilinçli olarak kapsam dışı bıraktığı karar artık
uygulanıyor (Semih, 2026-09-14): Expo görünümünün gruplama ANAHTARI
`orderId`'den gerçek masaya (`tableId`, `V1-KIT-012`) geçer. Aynı masanın
birden fazla açık siparişi (ör. bir masaya iki ayrı tur gönderilmişse)
artık TEK kartta birleşir — bugün ayrı ayrı sipariş kartları olarak
görünüyorlardı. Masasız (paket/bar) siparişler hâlâ kendi `orderId`'lerine
göre ayrı ayrı gruplanır (birleştirilecek ortak bir anahtarları yok).

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — `groupByOrder`'ın
  `groupByTable`'a dönüşmesi (masalı siparişler `tableId`'ye, masasız
  siparişler kendi `orderId`'lerine göre gruplanır); `OrderGroupCard`'ın
  artık birden fazla `orderId` taşıyabilecek bir grup göstermesi.

## Out of scope

- `V1-KIT-012`/`V1-KDS-005`'in kendisi — bu görev yalnız gruplama
  ANAHTARINI değiştirir, etiketi zaten V1-KDS-005 halletti.
- Aynı masanın farklı biletlerini tek bir "iptal et" aksiyonunda
  birleştirmek — iptal hâlâ ticket bazlı kalır (bu davranış zaten
  bugünkü kodda da tickets[0] üzerinden çalışıyor, grup büyüse de
  değişmiyor).

## Dependencies

- V1-KIT-012
- V1-KDS-005

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (yeni
  test: aynı masaya ait iki farklı `orderId`'nin TEK kartta birleştiği;
  masasız iki farklı siparişin ayrı ayrı kart olarak kaldığı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: aynı masaya iki ayrı tur
  gönder, Mutfak ekranında tek kartta iki istasyon/bilet olarak
  göründüğünü doğrula.

## Handoff

- None
