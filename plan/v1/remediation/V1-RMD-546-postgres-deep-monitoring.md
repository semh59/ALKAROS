# V1-RMD-546 - Veritabanı derin izleme

- Task ID: V1-RMD-546
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

En yavaş sorgular, kilitler, bağlantı doluluğu ve tablo şişmesi veritabanının kendi istatistiklerinden izlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-546-postgres-deep-monitoring.md`
- `evidence/V1-RMD-546/**`
- `src/Modules/Observability/PostgresInsight/**`
- `tests/Modules/Observability/PostgresInsight/**`
- `database/migrations/V1/V1-RMD-546/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `pg_stat_statements` ile en yavaş ve en sık sorgular (eklenti yoksa kapalı gösterilir); eklentisiz çalışan sorgular, kilit zincirleri, uzun işlemler, bağlantı doluluğu, ölü satır oranı. Bu parça yalnız okuma ve olay üretimi yapar; tablo yazmaz (göç gerekmez), Host işi V1-RMD-558 zincirinde bağlanır.
- Eklentinin açılması dağıtım yapılandırmasında (yeniden başlatma gerektiren) ayrı ve açık bir adımdır; uygulama kullanıcısı yetkisi doğrulanır.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Eklenti yokken sistem hata vermeden "kapalı" gösterir.
2. Yapay yavaş sorgu 1 dakika içinde listede görünür.
3. Çıktılar `evidence/V1-RMD-546/` altındadır.

## Handoff

- None
