# V0-LIC-001 - Define offline-safe licensing contract

- Task ID: V0-LIC-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:II.2.24
- PDF:III.26

## Goal

Tek seferlik license activation, machine binding, offline authorization, transfer, support update ve failure davranışını
tanımlamak.

## Owned surface

- `docs/licensing/licensing-contract.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Lisans imzası/karma, şube sınırı, kurulum aktarımı, saat geri alma, çevrimdışı işlem ve kurtarma sahipliği.

## Out of scope

- Abonelik faturalandırması, DRM bypass veya uzaktan kapatma davranışı işletme tarafından onaylanmadı.

## Dependencies

- V0-ARC-002
- V0-CMP-003

## Deliverables

- V0-LIC-001 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Lisanslama hizmetinin kaybı, ana restoranın faaliyetlerini beklenmedik bir şekilde durduramaz; geçersiz lisans
  davranışı ve kurtarma açıktır.

## Handoff

- V20-LIC-001
- V20-LIC-002
