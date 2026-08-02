# V1-FND-012 - Align the runtime migration manifest

- Task ID: V1-FND-012
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C1
- CORR:C33

## Goal

Runtime migration manifestini yalniz disk uzerinde up/down script ciftleri
bulunan pozisyonlarla eslemek; planlanan gelecek schema'yi calisir manifest
gibi gostermemek.

## Owned surface

- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md`
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md`
- `evidence/V1-FND-012/**`

## In scope

- Runtime manifestten SQL dosyasi olmayan pozisyonlari cikarmak ve kaydetmek.
- Gercek migration seti icin exact count, position, up/down composition ve
  forward execution testlerini guncellemek.
- order.json sahipligini bu remediation gorevine devretmek ve sonraki schema
  gorevlerinin kendi SQL dosyalariyla birlikte manifest entry ekleme zorunlulugunu
  plan kaydina acikca yazmak.

## Out of scope

- Gelecek domain tablolarini uydurmak, bos SQL migration eklemek, host
  migration algoritmasini gevsetmek veya domain schema karari vermek.

## Dependencies

- V0-GOV-003
- V1-FND-002
- V1-FND-004

## Deliverables

- Yalniz uygulanabilir migration entry'lerini iceren runtime manifest,
  composition testleri ve sahiplik devri kaydi.

## Acceptance evidence

- Manifestteki her position icin tam bir up/down script cifti bulunur.
- Host, bos PostgreSQL 18 veritabaninda manifestteki tum SQL dosyalarini calistirir ve kaydeder.
- SQL dosyasi olmayan planlanan position runtime manifestte yer almaz ve reddedilir.

## Handoff

- V1-IAM-004
- V20-MIG-001
