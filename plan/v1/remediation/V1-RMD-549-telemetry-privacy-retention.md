# V1-RMD-549 - İzleme verisi gizliliği ve saklama

- Task ID: V1-RMD-549
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

İzleme verisi kişisel veri, ödeme verisi veya girilen metin taşımaz; her tablonun saklama süresi tanımlıdır; süresi dolan veri silinir; ham tıklama verisi yedeğe girmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-549-telemetry-privacy-retention.md`
- `evidence/V1-RMD-549/**`
- `src/Modules/Observability/TelemetryPrivacy/**`
- `tests/Modules/Observability/TelemetryPrivacy/**`
- `database/migrations/V1/V1-RMD-549/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İzin verilen alan listesi (allowlist); listede olmayan alan reddedilir; mevcut ObservabilityRedactionHook ve gizli kalıp tarayıcısı yeniden kullanılır; telefon, e-posta, TCKN, IBAN kalıpları eklenir.
- Saklama: ham olay 30 gün, saatlik özet 13 ay, hata olayı 90 gün; silme işi ve denetim kaydı; onaylı saklama kataloğuna yeni sınıflar eklenir.
- Ham tıklama tabloları kalıcı günlük tutmayan (UNLOGGED) olur ve saatlik yedekten hariç tutulur.

## Out of scope

- Aydınlatma metni (karar kaydında açık madde).

## Dependencies

- V1-RMD-550
- V1-RMD-551

## Acceptance evidence

1. Sızıntı testi: 12 kişisel veri kalıbı örneğinin hiçbiri depoya yazılmaz.
2. Saklama işi 30 günü aşmış satırların %100'ünü siler, süresi dolmamışlara dokunmaz.
3. Ham tablo adı kuralı (`raw_` öneki) kodda tanımlıdır ve testlidir; tabloların yedekten çıkarılması ve silme işinin zamanlanıp denetim kaydı yazması, tabloları oluşturan V1-RMD-533 ve V1-RMD-541 kabul koşullarındadır.
4. Çıktılar `evidence/V1-RMD-549/` altındadır.

## Handoff

- None
