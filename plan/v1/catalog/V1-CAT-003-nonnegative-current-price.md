# V1-CAT-003 - Independently verify nonnegative current price

- Task ID: V1-CAT-003
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

zero/positive price'ın kabul edildiğini ve negative price'ın domain ile PostgreSQL sınırlarında atomik olarak
reddedildiğini bağımsız doğrulamak.

## Owned surface

- C70 (2026-08-16) konsolidasyonu: production yüzeyleri (current-price invariant ürünü) tam yüzey devri ile
  V1-RMD-001'e taşındı; bu historical task NotApplicable kapalı kalır.
- `evidence/V1-CAT-003/**`

## In scope

- `CODE-014` için current-price nonnegative invariant'ını Product, PostgreSQL repository ve additive migration
  sınırlarında uygulamak.

## Out of scope

- Global MigrationComposition manifest, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-CAT-001
- V1-FND-021

## Onay

NotApplicable — C70 (2026-08-16) kullanıcı onaylı konsolidasyon: bulgu yüzeyleri ve kabul koşulları tam
yüzey devri ile V1-RMD-001'e taşındı. Approved by Semih — Founder/Product Owner — 2026-08-16.
Karar geçerlidir, yeni provenance paketi beklenmez.

## Deliverables

- Current-price implementation/migration diff'i, domain/repository tests ve raw transcript.

## Acceptance evidence

- Negative current price domain ve database katmanlarında reddedilir; zero/positive kabul edilir.
- Forward/down migration lifecycle, focused tests ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045
