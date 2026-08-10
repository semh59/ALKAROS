# V13-QNB-003 - Implement QNB incoming invoice retrieval

- Task ID: V13-QNB-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Gelen provider belgelerini bir kez özel, değişmez alım kayıtlarına alın.

## Owned surface

- `src/Modules/Invoicing/Qnb/Incoming/**`, `tests/Modules/Invoicing/Qnb/Incoming/**`,
  `database/migrations/V13/V13-QNB-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İmleç/checkpoint, provider ID benzersizliği, doğrulama durumu, ham belge koruması ve kopya işleme.

## Out of scope

- Tedarikçi eşleştirme, mal girişi ve borç kaydı.

## Dependencies

- V0-QNB-001
- V0-CMP-003
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Invoicing/Qnb/Incoming/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Özgeçmişleri checkpoint'den yeniden başlatın; yinelenen provider belgesi idempotent olarak mevcut giriş satırını
  döndürür; yeni satır üretilmez; geçersiz belge incelenebilir durumda kalır.

## Handoff

- V13-PUR-001
- V13-QNB-004
