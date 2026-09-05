# V1-RMD-067 - Build provenance and docker hygiene

- Task ID: V1-RMD-067
- Status: Done
- Assignee: 0e4d2094-eff5-490e-9402-056e17c86376
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`.dockerignore` ve `.gitignore` dosyalarına `.pnpm-store` kuralını eklemek, Release assembly informational version commit SHA'larını ve `build/provenance/provenance_manifest.json` dosyasını HEAD commit ile senkronize etmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-067-build-provenance-and-docker-hygiene.md`
- `.dockerignore`
- `.gitignore`
- `build/provenance/**`

## In scope

- `.dockerignore` ve `.gitignore` içine `**/.pnpm-store` eklemek.
- `build/provenance/provenance_manifest.json` dosyasını güncel dosya sayıları ve doğrulanmış plan hash'leri ile güncellemek.
- `verify_build_provenance.py` testlerinin geçmesini sağlamak.

## Out of scope

- Diğer derleme veya altyapı araçlarını değiştirmek.

## Dependencies

- V1-RMD-066

## Deliverables

- Temiz dockerignore/gitignore ve senkronize provenance manifesti.

## Acceptance evidence

- `pytest tests/Architecture/BuildProvenance/` 0 hata ile geçer.

## Handoff

- V1-GOV-037
