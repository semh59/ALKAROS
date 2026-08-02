# V12-FSC-001 - Implement FiscalDocument lifecycle

- Task ID: V12-FSC-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19

## Goal

Provider/device reference ve immutable request history ile sale, cancellation ve refund FiscalDocument kayıtlarını
kalıcılaştırmak.

## Owned surface

- `src/Modules/Fiscal/DocumentLifecycle/**`, `tests/Modules/Fiscal/DocumentLifecycle/**`,
  `database/migrations/V12/V12-FSC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kanonik geçişler, belge türü/stratejisi, bill/payment/geri ödeme kaynağı bütünlüğü ve sterilize edilmiş yük depolama.

## Out of scope

- Payment onay aktarımı ve QNB invoice oluşturma.

## Dependencies

- V0-CMP-001
- V0-DAT-002
- V1-SEC-002

## Deliverables

- `src/Modules/Fiscal/DocumentLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tam olarak tek bir geçerli kaynak ilişkisi uygulanır; reddedilen/bilinmeyen düzenleme, önceki denemelerin yeniden
  yazılmasına gerek kalmadan kurtarılabilir.
- Belge numarası strateji/cihaz bazında boşluksuz monoton artar; numara çakışması veya tekrar kullanım olmaz
  (yalnızca iptal/red numarayı yakmaz).

## Handoff

- V12-FSC-002
- V12-REC-001
