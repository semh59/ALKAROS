# V1-RMD-262 - Misafir sipariş kalemi notu sunucuda 200 karakterle sınırlanır; QR ve NFC test fixture'ları tamamlanır

- Task ID: V1-RMD-262
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

E2E ana planının açık bulgusu: kalem notu (`SpecialInstructions`) misafir
kanallarında (QR ve NFC) sunucuda hiç doğrulanmıyordu. İstemciler `maxlength=200`
uyguluyor ama sunucu istemciye güvenmemeli; doğrudan API çağıran biri sınırsız
uzunlukta not (`TEXT` sütunu) yazabiliyordu. Personel yolu zaten 1000 karakterle
sınırlı. Her iki misafir mağazası artık 200 karakteri aşan notu `ArgumentException`
ile reddeder; uç noktalar bunu mevcut eşlemeyle `400 VALIDATION_FAILED` ("İstek
doğrulanamadı.") olarak döndürür ve hiçbir sipariş/outbox kaydı oluşmaz.

Ek olarak iki test fixture'ının eski kaldığı görüldü: NFC fixture'ı `118`
(stok kalemi yeniden sipariş noktası) migrasyonunu, QR fixture'ı ise stok/envanter
migrasyonlarını içermiyordu; bu yüzden ilgili mutlu yol testleri temiz ağaçta bile
`503 DATABASE_UNAVAILABLE` ile kırılıyordu. Eksik migrasyonlar fixture'lara eklendi.

## Owned surface

- `plan/v1/remediation/V1-RMD-262-guest-order-line-note-server-cap.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/NfcOrdering/NfcOrderingStore.cs
  (NFC sahibi görevlerde kalır — yalnız not uzunluğu sabiti ve doğrulaması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/QrOrdering/PendingOrders/QrPendingOrderStore.cs
  (QR sahibi görevlerde kalır — yalnız not uzunluğu sabiti ve doğrulaması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/NfcOrdering/NfcOrderingHttpTests.cs
  (aynı sahiplikte — 200/201 sınır testi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/NfcOrdering/ALKAROS.Host.Experience.NfcOrdering.Tests.csproj
  (yalnız eksik 118 migrasyonu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs
  (aynı sahiplikte — 200/201 sınır testi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
  (yalnız eksik envanter migrasyonları)

## In scope

1. QR ve NFC mağazalarında 200 karakter üstü kalem notu reddi.
2. Her iki kanal için 200 kabul / 201 red testleri (Türkçe gövde, İngilizce sızıntı yok).
3. Eksik fixture migrasyonlarının eklenmesi.

## Out of scope

- Personel yolunun 1000 sınırı ve DB sütun kısıtı (ayrı karar).
- NFC eşzamanlı aynı gönderim testinde bir kez görülen aralıklı 503 (sonraki 3 koşuda
  geçti); ayrı inceleme gerektirir.

## Dependencies

- V1-RMD-261

## Acceptance evidence

- Host.Experience.QrOrdering testleri (UTF8 Postgres 18): 32/32; NFC: yeni testler
  geçti, tek aralıklı eşzamanlılık testi yukarıda belirtildi.
- `python tools/plan-audit/plan_audit_tool.py validate` ve
  `python tools/consistency-audit/consistency_audit.py` çalıştırıldı.

## Handoff

- None
