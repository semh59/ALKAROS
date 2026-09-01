# V1-RMD-088 - Deployment infrastructure performance tuning

- Task ID: V1-RMD-088
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01
- PDF:I.38
- PDF:I.45.1
- EXT:POSTGRESQL-18.4

## Goal

ALKAROS dağıtımı PostgreSQL ve .NET çalışma zamanını ayarlanmamış varsayılan değerlerle çalıştırıyor: `shared_buffers` 128 MB, `work_mem` 4 MB, `random_page_cost` 4 (dönen disk varsayımı), `jit` açık (sub-ms OLTP sorgularında zarar), `wal_compression` kapalı, konteyner kaynak sınırı yok, .NET server GC doğrulanmamış. Bu görev adanmış tek lokanta donanımı için gerekçeli bir PostgreSQL ayar dosyası, Compose çalışma zamanı bayrakları ve kaynak sınırları ekler ve yük testi harness'i ile öncesi/sonrası ölçüm yapar.

## Owned surface

- `plan/v1/remediation/V1-RMD-088-deployment-infrastructure-performance-tuning.md`
- `deploy/docker/postgresql.tuned.conf` ayar dosyası.
- `compose.yaml` içindeki `postgres` ve `host` servis ayarları (config dosyası, çalışma zamanı bayrakları, kaynak sınırları).
- `docs/performance/infra-tuning.md` gerekçe ve ölçekleme kılavuzu.
- `evidence/V1-RMD-088/**` altındaki öncesi/sonrası ham çıktı.

## In scope

- `deploy/docker/postgresql.tuned.conf`: `shared_buffers`, `effective_cache_size`, `work_mem`, `maintenance_work_mem`, `max_connections`, `random_page_cost`, `effective_io_concurrency`, `wal_compression`, `checkpoint` ve `autovacuum` ayarları; her satırda değer gerekçesi ve donanıma göre ölçekleme formülü.
- `compose.yaml`: `postgres` servisinin ayar dosyasını yüklemesi, `shm_size` ve `deploy.resources` sınırları; `host` servisine `DOTNET_gcServer`, `DOTNET_GCDynamicAdaptationMode`, `DOTNET_TieredPGO` bayrakları ve `deploy.resources` sınırları.
- `docs/performance/infra-tuning.md`: her parametre için gerekçe, RAM'e göre yeniden boyutlandırma, uygulama adımları ve öncesi/sonrası ölçüm.
- `tools/load-test/load_test.py` ile ayar öncesi ve sonrası okuma senaryosu taraması; sonuçların karşılaştırılması.

## Out of scope

- `synchronous_commit` kapatmak; POS onaylı siparişi kaybedemez, bu ayar `on` kalır.
- Yazma yolu ve production boyutlu veri altında yük testi; `V15-PER-001` kapsamındadır.
- `src/**` uygulama kodu veya bağlantı dizesi oluşturma kodu; ayar yalnızca çalışma zamanı ve dağıtım katmanında yapılır.

## Dependencies

- V1-GOV-052

## Deliverables

- `deploy/docker/postgresql.tuned.conf` ve güncellenmiş `compose.yaml`.
- `docs/performance/infra-tuning.md` gerekçe ve ölçekleme kılavuzu.
- `evidence/V1-RMD-088/` altında öncesi/sonrası tarama çıktısı ve karşılaştırma.

## Acceptance evidence

- Ayarlı PostgreSQL ve `host` servisi `docker compose up` ile sorunsuz başlar; `pg_settings` ayarlı değerleri döndürür.
- Yük testi harness'i ayar sonrası yeniden çalışır; okuma senaryosu p95/p99 değerleri regresyon göstermez ve `V15-PER-001` hedefini karşılamaya devam eder.
- Öncesi/sonrası karşılaştırması ve her parametrenin gerekçesi `evidence/V1-RMD-088/` ve `docs/performance/infra-tuning.md` altında yazılıdır.

## Handoff

- V1-GOV-053
