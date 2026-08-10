# V1-TBL-002 - Implement transactional table transfer

- Task ID: V1-TBL-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

History'yi koruyarak open operational Order/Bill ilişkisini Table'lar arasında taşımak.

## Owned surface

- `src/Modules/TableManagement/TableTransfer/**`, `tests/Modules/TableManagement/TableTransfer/**`,
  `database/migrations/V1/V1-TBL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Unpaid Bill için tek transaction'da kaynak/hedef doğrulama, optimistic concurrency, history ve audit emission.

## Out of scope

- Çoklu table birleştirme ile pending, unknown veya partially-paid payment politikası; bunların sahibi `V12-TBL-001`dir.

## Dependencies

- V1-TBL-001
- V1-ORD-001
- V1-BIL-001
- V1-FND-005

## Deliverables

- `src/Modules/TableManagement/TableTransfer/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Başarılı aktarım Order/Bill kimliklerini korur; meşgul hedef veya eski sürüm, kısmi işaretçi değişiklikleri olmadan
  başarısız oluyor.
- Payment verisi bulunan Bill bu V1 komutuyla taşınamaz ve typed payment-policy-required sonucu verir.

## Handoff

- V1-TBL-003
- V1-TBL-005
- V12-TBL-001
