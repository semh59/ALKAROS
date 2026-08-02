# V12-MCD-001 - Implement MealCardPayment details

- Task ID: V12-MCD-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.14
- PDF:II.3.10
- PDF:II.5.10
- PDF:III.17

## Goal

Onaylanmış bir MealCard payment için provider, gross, commission, deduction ve net receivable alanlarını
kalıcılaştırmak.

## Owned surface

- `src/Modules/MealCard/Payments/**`, `tests/Modules/MealCard/Payments/**`, `database/migrations/V12/V12-MCD-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Her Payment için bir alt tür satırı, tutar formülü, provider referansı ve Pending durum.

## Out of scope

- Yerleşim gruplaması ve provider taşıma.

## Dependencies

- V12-PAY-001
- V12-PAY-002
- V0-MCD-001
- V0-DAT-002
- V1-SEC-002

## Deliverables

- `src/Modules/MealCard/Payments/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- MealCard olmayan payment ayrıntı satırını alamaz; net formül ve benzersizlik uygulanır.
- Net tutar = `gross` − `commission` − `deduction`; formül ve kaynak alanlar dosyada açıkça tanımlanır.
- `V0-MCD-001` tarihli/onaylı sonucu approved provider olmadığını gösterirse bu task `NotApplicable` olur; schema,
  success stub veya dead code oluşturulmaz.

## Handoff

- V12-MCD-002
