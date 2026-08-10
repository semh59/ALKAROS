# V0-REV-022 - Revalidate V0-ARC-003 decision evidence

- Task ID: V0-REV-022
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

Mevcut `docs/architecture/idempotency-inbox-outbox.md` Markdown kararındaki eksik kanıtı additive ve
tek-sorumluluklu bir supplement ile yeniden doğrulamak; eski artifact veya
`V0-ARC-003` görev gövdesini değiştirmemek.

## Owned surface

- `docs/decision-revalidation/V0-REV-022-v0-arc-003-decision-supplement.md`
- `evidence/V0-REV-022/**`

## In scope

- Inbox/outbox/idempotency karar provenance'i ve named architecture approver kanıtı.
- Tarihli source packet, erişim tarihleri, named approver, seçilen sonuç,
  reddedilen alternatifler ve etkilenen exact Task ID'leri kaydetmek.
- Supplement sonucunu mevcut Markdown kararıyla `confirms`, `supersedes`
  veya `conflicts` olarak sınıflandırmak.

## Out of scope

- `docs/architecture/idempotency-inbox-outbox.md`, `V0-ARC-003` görev Markdown'ı, PDF kaynak dosyası,
  üretim kodu, test, migration, gate veya başka decision artifact'i değiştirmek.
- Master PDF ile `plan/PDF_SOURCE.md` yalnız immutable historical trace girdisidir;
  current source packet, named approval veya remediation authority yerine geçmez.
- Kanıt gelmeden business, legal, provider veya teknik sonuç varsaymak.

## Dependencies

- V0-GOV-035

## Blocker

- Inbox/outbox/idempotency karar provenance'i ve named architecture approver kanıtı.
- Blocker ancak bu özel boşluğu kapsayan tarihli source packet ile approver'ın
  ad-soyad, kurum/rol ve onay tarihini içeren yazılı kanıt workspace'e
  alındıktan sonra kaldırılabilir.

## Deliverables

- `docs/decision-revalidation/V0-REV-022-v0-arc-003-decision-supplement.md` yolunda tek additive decision supplement.
- Kaynak snapshot/hash'i, approval transcript'i ve doğrulama çıktıları.

## Acceptance evidence

- Blocker'daki özel kanıt açığı gerçek kaynak ve named approval ile kapanır.
- Supplement seçilen sonuç, reddedilen alternatifler, gerekçe, erişim tarihleri
  ve etkilenen exact Task ID'leri içerir.
- `docs/architecture/idempotency-inbox-outbox.md` ve `V0-ARC-003` dosyalarının before/after hash'i aynıdır.
- Supplement dışında yeni decision record sayısı `0` olur.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0`
  verir; kanıtlar yalnız `evidence/V0-REV-022/**` altındadır.

## Handoff

- V0-GOV-036
