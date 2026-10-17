# V13-HUG-004 - Implement Token/Beko terminal totals reconciliation

- Task ID: V13-HUG-004
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
- CORR:C98

## Goal

**Retarget notu (2026-09-17, `V0-GOV-064`/CORR:C98):** hedef cihaz Hugin
T300 değil, Token/Beko (300 TR / X30 TR). Task ID değişmedi.

Yerel onaylı/iade edilmiş kart işlemlerini terminalin doğrulanmış toplamları veya işlem sorgu kaynağıyla karşılaştırın.

## Owned surface

- `src/Modules/Reconciliation/TokenTotals/**`, `tests/Modules/Reconciliation/TokenTotals/**`,
  `database/migrations/V13/V13-HUG-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Dönem/kesim kimliği, terminal referans eşleştirmesi, eksik/ekstra işlem ve sapma tespiti; case üretimi
  `V13-REC-001` API'si üzerinden.

## Out of scope

- Doğrulanmış Token/Beko contract dışındaki banka ödemesi.
- Araştırma notu (2026-10-17, V1-RMD-584), kayıt durumunu ve kararı değiştirmez:
- Token belgeleri: gün sonu/Z raporu entegrasyondan tetiklenemez. Z-Report Service (`x-platform/z-report-service/gelistirici-dokumani`) Z verisini okur ve `zNo` başına ödeme tipi bazında toplam verir, işlem satırı vermez; test adresi https://test-gmu-report.devtokeninc.com/zreport/web/ecr/api/v1/ ; yetkilendirme/erişim süreci yazılmamış (2026-10-17).
- Karar bekliyor: mutabakat kapsamının Z toplamı düzeyine indirilmesi veya NotApplicable.

## Dependencies

- V13-HUG-001
- V13-HUG-003
- V13-REC-001
- V0-HUG-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Reconciliation/TokenTotals/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bilinen test periyodu sıfır farkla uzlaşır; enjekte edilen eksik/ekstra işlem, izlenebilir bir vaka oluşturur.
- `V13-REC-001` tarihli `NotApplicable` ise payment reconciliation case üretimi bu task kapsamında doğrulanmaz; Token/Beko
  terminal totals karşılaştırması kendi doğrulanmış toplam/işlem sorgu kaynaklarıyla yine doğrulanır.

## Handoff

- V15-REC-001
