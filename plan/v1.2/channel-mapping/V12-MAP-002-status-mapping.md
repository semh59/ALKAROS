# V12-MAP-002 - Implement provider status mapping

- Task ID: V12-MAP-002
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Doğrulanan her Yemeksepeti status'ünü izinli internal command, explicit no-op veya typed unknown-status evidence
sonucuna eşlemek.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`
- `evidence/V12-MAP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (geri-tik olmadan): ALKAROS.slnx — yalnız yeni test projesi satırı ve `dotnet restore`'un ürettiği
  packages.lock.json.
- Tasarım notu: eşleme saf ve deterministik bir fonksiyondur; kalıcı tablo gerektirmez. Bilinmeyen durum için
  üretilen kanıtın kimliği yalnız sözlük sürümünden ve ham değerlerden türetilir. Bu yüzden sağlayıcı aynı durumu
  tekrar gönderdiğinde aynı kanıt çıkar ve kanıtı saklayan taraf (V12-ONL-001 inbox kaydı / V12-ONL-003) onu bir kez
  kaydeder.
- Kaynak notu: durum sözlüğü (RECEIVED, READY_FOR_PICKUP, DISPATCHED, CANCELLED, DELIVERED) ve teslimat türleri
  (VENDOR_DELIVERY, LOGISTICS_DELIVERY) yalnız herkese açık Partner API v2.0.2 belgesinden ve partner-picking SSS
  sayfasından alındı. Sandbox doğrulaması yoktur (V0-YSP-001 `Blocked`, `V12-GOV-004` waiver'ı).

## In scope

- Provider vocabulary version, integration-kind differences, cancellation reason ve unknown status evidence.

## Out of scope

- Webhook authentication, transport retry ve ReconciliationCase oluşturma.

## Dependencies

- V0-YSP-001
- V0-DOM-001

## Deliverables

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**` altında Goal kapsamını uygulayan production code ve
  task-specific automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Belgelenen her provider status'ünün tek sonucu vardır; unknown status Order'ı değiştirmez ve idempotent evidence event
  üretir.
- Kapanış kanıtı (2026-09-25): yeni test projesi 32/32 yeşil. Belgelenen beş durumun iki teslimat türüyle
  oluşturduğu on çiftin her biri tek bir sonuca eşlenir. RECEIVED `AcceptIncomingOrder`, CANCELLED `CancelOrder`
  (kimin iptal ettiği, gerekçe ve teslim sonrası bayrağıyla) üretir. Restoranın kendi gönderdiği durumun yankısı,
  kuryeye teslim ve platform teslimi açık no-op'tur. Belgede olmayan birleşimler (restoran teslimatında
  READY_FOR_PICKUP ve DELIVERED), küçük harfli veya bilinmeyen durumlar ve bilinmeyen teslimat türü tipli unknown
  döner. Unknown hiçbir komut üretmez; kanıt kimliği aynı durum için tekrarlarda ve 64 paralel çağrıda aynı kalır.
  Aşırı uzun veya kontrol karakterli sağlayıcı değerleri kırpılıp temizlenir.
- Mutasyon kontrolü (geri alındı, dosya birebir eşleşti): teslimat türü READY_FOR_PICKUP için yok sayılınca 1,
  unknown no-op'a çevrilince 10, kanıt kimliği deterministik olmaktan çıkınca 2, teslimat türü büyük/küçük harf
  duyarsız yapılınca 1 test kırmızıya döndü.
- Kanıt: `evidence/V12-MAP-002/`.

## Handoff

- V12-ONL-003
