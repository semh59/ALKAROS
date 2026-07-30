# V1-KIT-004 - Implement physical print retry safeguards

- Task ID: V1-KIT-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.16-I.20
- PDF:II.2.13
- PDF:II.3.13-II.3.14
- PDF:II.5.7-II.5.8
- PDF:II.8
- PDF:III.16

## Goal

Send/ack crash window'u explicit Unknown state ve operator-controlled reprint semantiğiyle yönetmek.

## Owned surface

- `src/Modules/Kitchen/PhysicalPrintRecovery/**`, `tests/Modules/Kitchen/PhysicalPrintRecovery/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Belirsiz teslimat, yeniden basılan etiketleme, operatör onayı ve mükerrer risk denetimi.

## Out of scope

- Mantıksal kuyruk oluşturma ve yazıcı yapılandırması.

## Dependencies

- V1-KIT-003
- V0-PRN-001

## Deliverables

- `src/Modules/Kitchen/PhysicalPrintRecovery/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Cihaz gönderildikten sonra ancak yerel işlemeden önce çökme hiçbir zaman tam olarak bir kez otomatik talepte
  bulunulmaz; kurtarma açık bir güvenli politika gerektirir ve denetlenir.

## Handoff

- V15-RUN-001
