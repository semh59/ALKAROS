# V1-RMD-533 - Hata olayı çekirdeği

- Task ID: V1-RMD-533
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Her hata katman ve şiddetle tek bir hata olayı olarak kaydedilir; parmak izi mevcut uyarı çatısının tekilleştirme anahtarı olur; yazım toplu ve sınırlıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-533-error-event-core.md`
- `evidence/V1-RMD-533/**`
- `src/Modules/Observability/ErrorEvents/**`
- `tests/Modules/Observability/ErrorEvents/**`
- `database/migrations/V1/V1-RMD-533/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Hata olayı modeli, parmak izi (sayı, kimlik, değişken metin normalize), `observability.error_events` ve `error_occurrences` (ayrı ay bölümlemeli).
- Tekilleştirme veritabanında; mevcut uyarı çatısına `RaiseAlert` ile bağlanır; onay durumu, iz kimliği ve sentetik işareti şemada vardır.
- Sınırlı bellek kuyruğu, 1-5 sn'lik toplu ve parmak izine göre birleştirilmiş yazım; yazma hatası ana işlemi bozmaz; düşen olay sayılır ve gösterilir.
- Sunucu saati esastır; olay kimliği ile tekrar gönderim yinelenmez.

## Out of scope

- Dedektörler; bildirim; ekran.

## Dependencies

- V1-RMD-549
- V1-RMD-551

## Acceptance evidence

1. Fırtına testi: aynı hatanın saniyede 500 oluşu en çok 1 yazım/sn/parmak izi ve ana havuzdan 0 bağlantı tüketir.
2. Parmak izi: kimlik ve sayı farkı olan 20 örnek tek satıra düşer.
3. Kuyruk dolunca düşen olay sayacı artar ve ekrandan okunur.
4. Hata olayı tabloları saklama kataloğuna (V1-RMD-549 `TelemetryRetention`) eklenir; silme işi zamanlanır ve her çalışma denetim kaydı yazar.
5. Gerçek Postgres testi ve mutasyon kanıtı.
6. Çıktılar `evidence/V1-RMD-533/` altındadır.

## Handoff

- None
