# V1-FND-010 - Verify shared integration-test fixture ownership

- Task ID: V1-FND-010
- Status: Done
- Assignee: opencode-v1-fnd-010
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C30

## Goal

Mevcut shared integration-test fixture dosyalarının üretim testlerindeki kullanımını, commit provenance'ını ve tek task
sahipliğini doğrulamak; doğrulanmayan yardımcıyı kabul edilmiş foundation kanıtı saymamak.

## Owned surface

- `tests/BuildingBlocks/TestHelpers/ALKAROS.TestHelpers.csproj`
- `tests/BuildingBlocks/TestHelpers/Fixtures/PgTestDatabase.cs`
- `tests/BuildingBlocks/TestHelpers/Fixtures/RecordingResource.cs`
- `tests/BuildingBlocks/TestHelpers/Fixtures/SimulatedFailures.cs`
- `evidence/V1-FND-010/**`
- Bu görev, fixture tüketicilerinin production testlerini veya başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Her fixture dosyasının hash'i, oluşturan commit'i, kullanan test projesi ve gerçek test sonucu.
- Ortak fixture sınırının yalnız integration-test altyapısı olduğunu kanıtlayan dependency ve coverage kaydı.
- Kanıt yoksa kabul edilebilir yeniden-atama veya silme planını, ayrı implementation task kimliği gerektirecek biçimde
  kaydetmek.

## Out of scope

- Fixture davranışını değiştirmek, test assertion'larını zayıflatmak, mock-success akışı eklemek veya başka task'ın
  testini
  sahiplenmek.

## Dependencies

- V1-FND-001

## Deliverables

- Her fixture için commit SHA, SHA-256, kullanıcı test projeleri ve exit code içeren `evidence/V1-FND-010/**` kaydı.
- Tek sahiplik karar kaydı veya yeni implementation task'ına devri zorunlu kılan açık blocker.

## Acceptance evidence

- Dört dosyanın her biri tek bu task'ın owned surface kaydına eşleşir; her kullanan test projesi listelenir ve gerçek
  test
  transcript'i aynı hash'e bağlanır.
- Kanıtlanmamış helper, `Done` foundation kanıtı veya başka task'ın örtük yüzeyi olarak kabul edilmez.

## Handoff

- GATE-V1-EXIT
