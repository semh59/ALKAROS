# V15-REC-001 - Implement unified reconciliation read model

- Task ID: V15-REC-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.21
- PDF:II.3.15
- PDF:II.5.12
- PDF:II.6.11
- PDF:III.23

## Goal

payment, mali, QNB, çevrimiçi, yemek kartı, cash ve satın alma genelinde açık vakalar için tek bir okuma modeli
oluşturun.

## Owned surface

- `src/Modules/Reconciliation/DashboardReadModel/**`, `tests/Modules/Reconciliation/DashboardReadModel/**`,
  `database/migrations/V15/V15-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kaynak çifti ekranı, önem derecesi, yaş, sahiplik, sonraki eylem ve rebuild.

## Out of scope

- Vaka çözümü komutları ve alert teslimi.

## Dependencies

- V12-REC-001
- V13-QNB-004
- V13-PUR-001
- V14-REC-001
- V13-ACC-007
- V0-DAT-004

## Deliverables

- `src/Modules/Reconciliation/DashboardReadModel/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Okuma modeli rebuild vaka sayılarını yeniden üretir; hiçbir vaka türü kaynak referanslarını veya gerekli sonraki
  eylemi kaybetmez.
- `V12-REC-001`, `V13-QNB-004` veya `V14-REC-001` kanıtlı `NotApplicable` ise ilgili vaka türleri okuma modelinde yer
  almaz; kalan kaynak türleri rebuild sayılarını yine yeniden üretir.

## Handoff

- V15-REC-002
- V15-OBS-002
