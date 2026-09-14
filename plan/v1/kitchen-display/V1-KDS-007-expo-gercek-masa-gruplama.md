# V1-KDS-007 - Expo görünümünü gerçek masaya göre gruplama

- Task ID: V1-KDS-007
- Status: Done
- Assignee: Claude Sonnet 5
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

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata** (doğrulandı).
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 160 passed (160)** — tüm proje, izole değil (2 yeni test:
  aynı `tableId`'ye sahip, farklı `orderId`'lerden gelen iki biletin TEK
  `.kitchen-order` kartında iki `.kitchen-station` olarak birleştiği;
  `tableId: null` olan iki farklı siparişin İKİ ayrı karta bölünmüş
  kaldığı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Uygulanan tasarım: `groupByOrder` → `groupByTable`, anahtar
  `ticket.tableId ?? ticket.orderId` (masasız sipariş kendi orderId'sine
  düşer, önceki davranışla birebir aynı). "Açık sipariş" istatistiği
  artık kart sayısını saydığı için "Açık masa/sipariş" olarak yeniden
  etiketlendi (dürüstlük — bir kart artık birden fazla siparişi
  temsil edebiliyor). İptal aksiyonu hâlâ `tickets[0]` üzerinden çalışıyor
  (Out of scope'ta belirtildiği gibi, bilinçli olarak değiştirilmedi).
- Semih'in elle deneyebileceği senaryo: aynı masaya iki ayrı tur
  gönder, Mutfak ekranında tek kartta iki istasyon/bilet olarak
  göründüğünü doğrula.

## Handoff

- None
