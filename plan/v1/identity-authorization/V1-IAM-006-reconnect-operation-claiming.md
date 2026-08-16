# V1-IAM-006 - Independently verify reconnect operation claiming

- Task ID: V1-IAM-006
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

aynı operation için concurrent reconnect çağrılarında applied count toplamının tam `1` olduğunu bağımsız PostgreSQL
interleaving'iyle doğrulamak.

## Owned surface

- C70 (2026-08-16) konsolidasyonu: production yüzeyleri (`DeviceSessionService.cs`, ilgili tests) tam yüzey devri ile
  V1-RMD-001'e taşındı; bu historical task NotApplicable kapalı kalır.
- `evidence/V1-IAM-006/**`

## In scope

- `CODE-006;CODE-007;CODE-019` için DeviceSessionService write paths'ini tek integration owner olarak atomikleştirmek.
- Reconnect claim, revocation linearization ve lifetime policy'nin task-owned code/test davranışını uygulamak.

## Out of scope

- Owned surface dışındaki device-session migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-003

## Onay

NotApplicable — C70 (2026-08-16) kullanıcı onaylı konsolidasyon: bulgu yüzeyleri ve kabul koşulları tam
yüzey devri ile V1-RMD-001'e taşındı. Approved by Semih — Founder/Product Owner — 2026-08-16.
Karar geçerlidir, yeni provenance paketi beklenmez.

## Deliverables

- Tek DeviceSessionService integration diff'i, concurrent interleaving tests ve raw transcript.

## Acceptance evidence

- Reconnect applied operation listesi yalnız transaction içinde kazanılan claims'ten oluşur.
- Revoke sonrası stale authentication/reconnect success dönmez; non-positive veya policy-exceeding lifetime reddedilir.
- Focused tests ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045
