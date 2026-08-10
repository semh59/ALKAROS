# V13-QNB-004 - Implement QNB invoice reconciliation

- Task ID: V13-QNB-004
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

Gönderim, cancellation/correction, local/provider status ve incoming retrieval farkları için reconciliation oluşturmak.

## Owned surface

- `src/Modules/Reconciliation/QnbInvoices/**`, `tests/Modules/Reconciliation/QnbInvoices/**`,
  `database/migrations/V13/V13-QNB-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outgoing/cancellation query recovery, incoming checkpoint gap, case deduplication ve resolution evidence.

## Out of scope

- Provider taşıma uygulaması ve birleştirilmiş kontrol paneli.

## Dependencies

- V13-QNB-002
- V13-QNB-003
- V13-QNB-005
- V12-REC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Reconciliation/QnbInvoices/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Submission veya cancellation timeout asla status query öncesi yeniden gönderilmez; accepted/rejected/unknown sonucu
  local ve provider reference taşıyan tek açık case'e bağlanır.
- `V13-QNB-005` kanıtlı `NotApplicable` ise cancellation case'i üretilmez; aynı tarihli decision evidence kaydedilir ve
  submission/incoming reconciliation kapsamı test edilmeye devam eder.
- `V12-REC-001` kanıtlı `NotApplicable` ise QNB invoice reconciliation kendi submission/status kaynaklarıyla vaka
  üretmeye devam eder; payment reconciliation case'lerine bağımlılık beklenmez.

## Handoff

- V15-REC-001
