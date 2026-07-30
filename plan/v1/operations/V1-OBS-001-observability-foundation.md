# V1-OBS-001 - Implement observability correlation foundation

- Task ID: V1-OBS-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28

## Goal

V1 flow'ları için structured event contract, correlation/request ID ve bounded status-audit persistence eklemek.

## Owned surface

- `src/Modules/Observability/Foundation/**`, `tests/Modules/Observability/Foundation/**`,
  `database/migrations/V1/V1-OBS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Onaylanan politika tarafından yönlendirilen korelasyon yayılımı, temel sağlık status kataloğu, redaksiyon kancası ve
  kalıcı saklama politikası kimliği.

## Out of scope

- Tam alert kuralları, ölçüm arka ucu ve hassas yük şifrelemesi.

## Dependencies

- V1-FND-001
- V0-DAT-002
- V0-CMP-003

## Deliverables

- `src/Modules/Observability/Foundation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Order yazdırma kuyruğuna gönderme ID tek korelasyonla izlenebilir; sağlık status standarttır; gizli test işaretçisi
  düzeltildi; onaylanmış bir saklama politikası kimliği olmadan ısrar reddedilir.

## Handoff

- V15-OBS-001
- V15-OBS-002
- V15-OBS-003
