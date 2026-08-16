# V1-IAM-011 - Independently verify login-session integration

- Task ID: V1-IAM-011
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

başarılı login'in tek persisted, validated ve revocable credential lifecycle ürettiğini; failure'da orphan token/session
bırakmadığını bağımsız doğrulamak.

## Owned surface

- C70 (2026-08-16) konsolidasyonu: production yüzeyleri (login credential lifecycle ürünü) tam yüzey devri ile
  V1-RMD-001'e taşındı; bu historical task NotApplicable kapalı kalır.
- `evidence/V1-IAM-011/**`

## In scope

- `CODE-012;CODE-016` için single persisted/validated/revocable login credential lifecycle ve bounded work-factor
  contract'ını tek authentication integration diff'inde uygulamak.
- Success, failure ve revoke paths'ini task-owned tests ile doğrulamak.

## Out of scope

- Owned surface dışındaki password hashing, device-session migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-003
- V1-IAM-009

## Onay

NotApplicable — C70 (2026-08-16) kullanıcı onaylı konsolidasyon: bulgu yüzeyleri ve kabul koşulları tam
yüzey devri ile V1-RMD-001'e taşındı. Approved by Semih — Founder/Product Owner — 2026-08-16.
Karar geçerlidir, yeni provenance paketi beklenmez.

## Deliverables

- Authentication lifecycle integration diff'i, success/failure/revoke tests ve raw transcript.

## Acceptance evidence

- Raw credential is persisted/validated/revocable through one lifecycle; orphan issuer/session oluşmaz.
- Unknown/known failure work bounded contract'ı taşır; focused tests ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045
