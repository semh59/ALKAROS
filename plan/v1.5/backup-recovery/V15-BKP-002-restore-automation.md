# V15-BKP-002 - Implement isolated restore verification

- Task ID: V15-BKP-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.23
- PDF:III.25

## Goal

Isolated PostgreSQL instance'a restore işlemini otomatikleştirmek ve integrity/application smoke kontrollerini
çalıştırmak.

## Owned surface

- `src/Modules/Operations/RestoreVerification/**`, `tests/Modules/Operations/RestoreVerification/**`,
  `deployment/restore/**`, `database/migrations/V15/V15-BKP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Yapı seçimi, şifre çözme, geri yükleme, bütünlük sorguları, uygulama başlatma dumanı ve sonuç kaydı.

## Out of scope

- Production felaket kararı ve tam kurtarma tatbikatı.

## Dependencies

- V15-BKP-001
- V0-BKP-001
- V0-BKP-002

## Deliverables

- `src/Modules/Operations/RestoreVerification/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Planlanmış test, gerçek yapıyı geri yükler ve ölçülen süreyi kaydeder; bozuk yapıt, uygulama başlatılmadan önce
  başarısız olur.
- Ölçülen restore süresi ve doğrulama adımları, `V0-BKP-002` kararındaki onaylı RTO eşiğini karşılar.

## Handoff

- V20-DRL-001
