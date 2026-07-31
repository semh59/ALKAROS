# V20-REL-001 - Assemble release package

- Task ID: V20-REL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: release
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- EXT:CYCLONEDX-1.7
- EXT:SLSA-1.2

## Goal

Doğrulanmış ikili dosyalardan, yükleyici/güncelleyiciden, geçişlerden, yapılandırma şemasından ve belgelerden değişmez
bir release adayı oluşturun.

## Owned surface

- `release/candidate/**`
- Bu görev kaynak kodu veya üretilmiş artifact içeriğini elle değiştiremez.

## In scope

- Sürüm kimliği, yapı bildirimi, sağlama toplamları/imzalar, SBOM, yapılandırma şeması ve kaynağı.

## Out of scope

- Paket görevi içinde kapı onayı, production dağıtımı ve başarısız bileşenlerin yeniden oluşturulması.

## Dependencies

- V20-INS-002
- V20-DOC-001
- V20-DOC-002
- V0-ARC-008

## Deliverables

- Değişmez release aday ve doğrulama komutu.

## Acceptance evidence

- Bağımsız doğrulama, hashları/imzaları yeniden üretir ve paketlenmiş her bileşeni tek bir yapı kimliğine ve kaynak
  revizyonuna eşler.
- `V20-DOC-001` kanıtlı `NotApplicable` ise rol kılavuzu pakete dahil edilmez; kalan bileşenlerin doğrulama ve
  eşleme kuralı yine geçerlidir.
- `V20-DOC-002` kanıtlı `NotApplicable` ise teknik referans pakete dahil edilmez; kalan bileşenlerin doğrulama ve
  eşleme kuralı yine geçerlidir.

## Handoff

- V20-GAT-001
- V20-REL-002
