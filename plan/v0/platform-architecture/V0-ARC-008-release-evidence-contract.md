# V0-ARC-008 - Define release evidence contract

- Task ID: V0-ARC-008
- Status: Done
- Assignee: codex-v0-arc-008
- Work type: decision
- Surface state: Existing

## Source basis

- EXT:CYCLONEDX-1.7
- EXT:SLSA-1.2
- CORR:C16

## Goal

Release artifact, checksum, signing, CycloneDX SBOM ve SLSA provenance için tek evidence contract belirlemek.

## Owned surface

- `docs/architecture/release-evidence-contract.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Artifact identity, hash algorithm, signing authority, key custody, SBOM format, provenance level ve retention.

## Out of scope

- Build pipeline implementation, commercial certificate purchase ve release approval.

## Dependencies

- V0-ARC-004
- V0-ARC-005

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen release evidence schema, levels ve verification failure davranışı.

## Acceptance evidence

- Aynı release kimliği artifact, signature, SBOM ve provenance kayıtlarında deterministik olarak eşleşir.

## Handoff

- V20-REL-001
- V20-GAT-002
