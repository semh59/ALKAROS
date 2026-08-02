# V13-CST-001 - Implement customer PII boundary

- Task ID: V13-CST-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Field-level access policy ile PII sahibi boundary içinde minimum customer identity, tax ve contact alanlarını
kalıcılaştırmak.

## Owned surface

- `src/Modules/CustomerData/Profiles/**`, `tests/Modules/CustomerData/Profiles/**`,
  `database/migrations/V13/V13-CST-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Müşteri türü, vergi kimliği, iletişim alanları, saklama meta verileri ve rol tabanlı okumalar.
- Anonimleştirilmiş müşteri kaydına e-Fatura düzenlenemez; UBL zorunlu tanımlayıcı gereksinimleri V13-INV-002
  kapsamındadır.

## Out of scope

- Müşteri hesap bakiyeleri ve anonimleştirmenin yürütülmesi.

## Dependencies

- GATE-V13-ENTRY
- V0-CMP-003

## Deliverables

- `src/Modules/CustomerData/Profiles/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yetkisiz roller korumalı alanları okuyamaz; gerekli invoice kimliği geçerli kalır; isteğe bağlı PII geçersiz
  kılınabilir/küçültülebilir.
- Her PII alanı `V0-CMP-003` envanterindeki purpose, retention, access owner ve disposal sonucu ile bire bir eşleşir;
  envantersiz alan migration'a giremez.

## Handoff

- V13-CST-002
- V13-ACC-001
- V13-INV-002
