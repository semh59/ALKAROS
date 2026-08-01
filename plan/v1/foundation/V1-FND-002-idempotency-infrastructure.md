# V1-FND-002 - Implement the idempotency infrastructure

- Task ID: V1-FND-002
- Status: Planned
- Assignee: opencode-v1-fnd-002
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.11-I.15
- CORR:C4

## Goal

V0-ARC-003 sözleşmesindeki request-key validation, response replay, Inbox persistence ve Outbox dispatch altyapısını
uygulamak.

## Owned surface

- `src/BuildingBlocks/Idempotency/**`, `src/BuildingBlocks/Messaging/**`, `tests/BuildingBlocks/Idempotency/**`,
  `database/migrations/V1/V1-FND-002/**`
- Kapsam genişletme onayı (2026-08-01 kullanıcı talimatı): bu task'ın yeni projelerinin `ALKAROS.slnx` ve
  `build/project-manifest.json` içine kaydı; `Directory.Packages.props` içine `Npgsql` PackageVersion eklentisi.
- Kapsam genişletme onayı (2026-08-01 kullanıcı talimatı): `docs/data/migration-dependency-graph.md` (V0-DAT-001
  sahipliğinde kayıtlı güncelleme) ve `database/MigrationComposition/order.json` üzerinde bu task'ın migration
  pozisyon kaydı; `order.json` sahipliği V1-FND-004'ten devralınır (V1-FND-004 yüzey daraltma satırı).
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md` (yalnız Owned surface devir satırı)
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Idempotency (V0-ARC-003 §1): per-client key scope (client_id + operation_id), canonical SHA-256 request hash,
  aynı key/aynı hash response replay, aynı key/farklı hash IDEMPOTENCY_KEY_CONFLICT reddi, 24 saat retention sweep.
- Inbox (V0-ARC-003 §2): external callback persistence, (source, externalEventId) unique dedup, attempt_count,
  3 başarısız deneme sonrası dead-letter taşıma (poison event).
- Outbox (V0-ARC-003 §3): pending/dispatched/dead state, SKIP LOCKED ile tekil dispatch, at-least-once teslim,
  exponential backoff (max 3), process restart sonrası pending kayıtların kayıpsız yeniden ele alınması.
- SEC-002 kapsamı gereği webhook/Outbox payload'larında redaksiyon ve SensitiveEnvelope koruması.
- V1-FND-002 migration'ları: boş PostgreSQL 18'de ileri/geri kanıt; docs/data/migration-dependency-graph.md ve
  database/MigrationComposition/order.json pozisyon kaydı (V0-DAT-001 manifest aralığına göre).
- Database erişimi Npgsql üzerinden (Directory.Packages.props'a central PackageVersion eklentisi).

## Out of scope

- Transactional enqueue, commit-before-dispatch ve post-commit wake-up (V1-FND-006 kapsamı).
- Order'ye özel gönderme kuralları, provider yük eşlemesi, provider transport ve domain event mapping.

## Dependencies

- V1-FND-001
- V1-SEC-002
- V0-ARC-003

## Deliverables

- `src/BuildingBlocks/Idempotency/**` ve `src/BuildingBlocks/Messaging/**` altında Goal kapsamını uygulayan
  production code ve task-specific automated test assets.
- `tests/BuildingBlocks/Idempotency/**` içinde otomatik başarı, ret ve edge-case testleri (gerçek PostgreSQL 18
  integration dahil).
- `database/migrations/V1/V1-FND-002/**` altında ileri/geri migration ve graph/order.json pozisyon kaydı.
- `ALKAROS.slnx`, `build/project-manifest.json` ve `Directory.Packages.props` içinde yeni proje/paket kayıtları.

## Acceptance evidence

- Automated testler replay semantiğini ve process restart'ın pending Outbox kayıtlarını kaybetmediğini kanıtlar.
- Aynı key/farklı hash reddedilir (`IDEMPOTENCY_KEY_CONFLICT`); aynı key/aynı hash cache'lenmiş response'u döner.
- Aynı `(source, externalEventId)` ikinci kez işlenmez; 3 başarısız deneme sonrası kayıt dead-letter'a taşınır.
- Outbox dispatch exponential backoff (max 3) ile yeniden dener; başarıda `dispatched` olur; migration up/down
  boş PG18'de kanıtlanır.

## Handoff

- V1-FND-006
- V1-ORD-002
- V1-KIT-003
- V14-ONL-001
