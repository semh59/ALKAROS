# V14-CST-001 - Implement customer PII boundary

- Task ID: V14-CST-001
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

**Taslak notu (2026-09-18, `V14-GOV-001`):** `GATE-V14-ENTRY` hâlâ açık
olduğu için bu görev henüz `InProgress` alınamıyor. Semih'in onayıyla,
gerçek gate kapanmadan önce yalnız bir domain taslağı
`evidence/V14-GOV-001/customer-pii-draft/` altında yazıldı (bu görevin
Owned surface'ının DIŞINDA, ayrı/standalone bir proje olarak). Bu görev
gerçekten başladığında o taslak referans alınabilir, ama Acceptance
evidence yine `GATE-V14-ENTRY`'nin kapanmasını ve gerçek Owned surface'a
taşınmayı gerektiriyor.

## Owned surface

- `src/Modules/CustomerData/Profiles/**`, `tests/Modules/CustomerData/Profiles/**`,
  `database/migrations/V14/V14-CST-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Müşteri türü, vergi kimliği, iletişim alanları, saklama meta verileri ve rol tabanlı okumalar.
- Anonimleştirilmiş müşteri kaydına e-Fatura düzenlenemez; UBL zorunlu tanımlayıcı gereksinimleri V14-INV-002
  kapsamındadır.

## Out of scope

- Müşteri hesap bakiyeleri ve anonimleştirmenin yürütülmesi.

## Dependencies

- GATE-V14-ENTRY
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

- V14-CST-002
- V14-ACC-001
- V14-INV-002
