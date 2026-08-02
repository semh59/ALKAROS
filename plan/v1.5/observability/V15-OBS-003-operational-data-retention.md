# V15-OBS-003 - Implement observability retention and partitioning

- Task ID: V15-OBS-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28

## Goal

Korunan kayıtları silmeden health, alert-event, inbox/outbox ve high-volume audit support verisinin büyümesini
retention/partition kurallarıyla sınırlamak.

## Owned surface

- `src/Modules/Observability/Retention/**`, `tests/Modules/Observability/Retention/**`,
  `database/migrations/V15/V15-OBS-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bölümleme anahtarları, saklama sınıfları, temizleme işleri, yasal bekletmeler ve temizleme denetimi.

## Out of scope

- Müşteri PII anonimleştirme ve değişmez mali/denetim olayları.

## Dependencies

- V15-OBS-001
- V15-OBS-002
- V0-CMP-003
- V1-OBS-001

## Deliverables

- `src/Modules/Observability/Retention/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tutma testi yalnızca süresi dolmuş uygun bölümleri/satırları kaldırır; tutulan veya değiştirilemeyen veriler kalır;
  işin yeniden başlatılması güvenlidir.
- `V15-OBS-002` kanıtlı `NotApplicable` ise alert kaynaklı tutma senaryoları beklenmez; kalan bölüm/satır tutma ve
  idempotent restart davranışı yine doğrulanır.

## Handoff

- V20-MIG-001
