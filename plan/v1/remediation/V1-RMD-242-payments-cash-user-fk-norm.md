# V1-RMD-242 - Apply the identity.users FK norm to the V13 Payments/Cash schema

- Task ID: V1-RMD-242
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-RMD-189`/`V1-RMD-191` established and applied this codebase's own
consistent norm ("Cross-module FKs to identity.users are this codebase's
consistent norm, not an exception this schema was carved out of") to
identity's own authorization tables and to Billing. The newer V13
Payments/Cash schema (120, 122, 124, 126) never got the same treatment —
a bağımsız denetim ajanı (2026-09-18, tüm proje kod denetimi, database
migrations alanı) bunu tespit etti: `payment_status_history.changed_by`,
`cash_sessions.cashier_user_id`/`closed_by`/`reconciled_by`,
`cash_counts.counted_by`, `cash_transactions.recorded_by`,
`refund_intents.requested_by` — hiçbiri `identity.users`'a FK'li değil,
yani geçersiz/yanlış yazılmış bir `user_id` sessizce kaydedilebilir.

## Owned surface

- `database/migrations/V1/V1-RMD-242/**`
- `evidence/V1-RMD-242/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs — yeni migration
  pozisyonunun (131) kaydı (V13-CSH-001 emsaliyle aynı desen).

## In scope

- Yedi kolonun (yukarıda) hepsine `identity.users(user_id)`'a gerçek FK
  eklemek. `V1-RMD-191` emsaliyle aynı kural: "kim yaptı" (NOT NULL,
  authorized_by-benzeri) kolonlar RESTRICT (varsayılan) kalır
  (`cashier_user_id`, `counted_by`); "kim kapattı/onayladı/kaydetti"
  (NULL, created_by-benzeri) kolonlar `ON DELETE SET NULL` alır
  (`changed_by`, `closed_by`, `reconciled_by`, `recorded_by`,
  `requested_by`).
- `cash_transactions.recorded_by` üzerinde eksik olan index de eklenir
  (aynı denetimde bulundu).

## Out of scope

- Şema/tablo tasarımının başka hiçbir yönü.

## Dependencies

- V1-RMD-191

## Acceptance evidence

- Migration boş bir veritabanında ileri/geri (up/down) denenir; up sonrası
  var olmayan bir `user_id` ile insert gerçekten reddedilir (FK ihlali).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Cash + Payments modül/HTTP testleri) → yeşil (mevcut
  testler zaten gerçek seed edilmiş kullanıcılar kullanıyor, davranış
  değişmez).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None
