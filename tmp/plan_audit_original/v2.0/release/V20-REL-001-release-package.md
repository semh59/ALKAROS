# V20-REL-001 - Assemble release package

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: release

## Goal

Assemble one immutable release candidate from verified binaries, installer/updater, migrations, configuration schema and documentation.

## Owned surface

- `release/candidate/**`
- Bu görev kaynak kodu veya üretilmiş artifact içeriğini elle değiştiremez.

## In scope

- Version identity, artifact manifest, checksums/signatures, SBOM, configuration schema and provenance.

## Out of scope

- Gate approval, production deployment and rebuilding failed components inside the package task.

## Dependencies

- V20-INS-002, V20-DOC-001, V20-DOC-002

## Deliverables

- Immutable release candidate and verification command.

## Acceptance evidence

- Independent verification reproduces artifact hashes/signatures and maps every packaged component to one build identity and source revision.

## Handoff

- V20-REL-002 and all V20 validation tasks.
