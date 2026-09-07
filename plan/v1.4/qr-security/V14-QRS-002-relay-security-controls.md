# V14-QRS-002 - Implement QR relay authentication and abuse controls

- Task ID: V14-QRS-002
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Relay message authentication yapmak ve local command dispatch öncesi replay, rate-limit ve payload-size kontrollerini
uygulamak.

## Owned surface

- `src/Modules/QrOrdering/RelaySecurity/**`, `tests/Modules/QrOrdering/RelaySecurity/**`
- `database/migrations/V14/V14-QRS-002/**`
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Modules/QrOrdering/TokenLifecycle/QrOrderingModule.cs (V14-QRS-001
    sahipliğinde) — yalnız `IRelayNonceStore`/`RelayRequestValidator`
    kayıtları eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs
    (V1-FND-004/V1-IAM-025 sahipliğinde) — yalnız migration 081 kaydı
    eklendi.

## In scope

- İmza/anahtar rotasyonu, tek seferlik, zaman damgası penceresi, jeton başına/IP limitleri ve güvenli reddetme.
- "İmza/anahtar" = `V14-QRS-001`'in hashed table token'ı; bu görev onun
  üzerine tek-seferlik (nonce) replay koruması ve zaman damgası penceresi
  ekliyor — token'ın kendisi zaten rotasyon/iptal destekliyor (V14-QRS-001).
- Jeton-başına/IP oran sınırı ve payload-size sınırları: framework'ten
  bağımsız sabitler (`RelayAbusePolicy`) olarak tanımlandı — gerçek
  ASP.NET Core rate limiter policy'sine/endpoint filter'ına bağlanması,
  bunları gerçekten kullanacak endpoint'i açan görevin işi (`V14-QRO-001`).

## Out of scope

- QR order iş doğrulaması ve yerel ağ dağıtımı.
- Rate-limit/payload-size sabitlerinin gerçek bir HTTP endpoint'ine
  bağlanması (`V14-QRO-001`'in kapsamı — bu görevin `Owned surface`'i
  `src/Host/**`'e hiç dokunmuyor).

## Dependencies

- V14-QRS-001
- V0-QRG-001
- V1-FND-002

## Deliverables

- `src/Modules/QrOrdering/RelaySecurity/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tekrar oynatma ve değiştirilmiş veriler reddedilir; oran sınırı Order üretmez; Yerel hizmet, genel gelen uç noktayı
  göstermez.
- `RelayRequestValidator`: aynı (token, nonce) çifti ikinci kez `REPLAYED`
  ile reddedilir; farklı token'lar aynı nonce değerini bağımsız
  kullanabilir (`qr_ordering.relay_request_nonces`'ın birincil anahtarı
  `(token_id, nonce)`); ±2 dakika dışındaki zaman damgaları
  `TIMESTAMP_OUT_OF_WINDOW` ile reddedilir; bilinmeyen/süresi
  dolmuş/iptal edilmiş token, nonce/zaman damgası hiç kontrol edilmeden
  önce reddedilir (en ucuz kontrol önce).
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `ALKAROS.QrOrdering.RelaySecurity.Tests`: 7/7, gerçek Postgres'e karşı.
- `python tools/consistency-audit/consistency_audit.py`: yeni ihlal yok.
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata.

## Handoff

- V14-QRO-001
