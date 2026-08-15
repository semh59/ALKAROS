# V0-REV-002 - Revalidate V0-DAT-002 decision evidence

- Task ID: V0-REV-002
- Status: Done
- Assignee: Semih (product owner)
- Work type: decision
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

Mevcut `docs/data/canonical-value-catalog.md` Markdown kararındaki eksik kanıtı additive ve
tek-sorumluluklu bir supplement ile yeniden doğrulamak; eski artifact veya
`V0-DAT-002` görev gövdesini değiştirmemek.

## Owned surface

- `docs/decision-revalidation/V0-REV-002-v0-dat-002-decision-supplement.md`
- `evidence/V0-REV-002/**`

## In scope

- Canonical state catalog uzlaşması ve named domain/data approver kanıtı.
- Tarihli source packet, erişim tarihleri, named approver, seçilen sonuç,
  reddedilen alternatifler ve etkilenen exact Task ID'leri kaydetmek.
- Supplement sonucunu mevcut Markdown kararıyla `confirms`, `supersedes`
  veya `conflicts` olarak sınıflandırmak.

## Out of scope

- `docs/data/canonical-value-catalog.md`, `V0-DAT-002` görev Markdown'ı, PDF kaynak dosyası,
  üretim kodu, test, migration, gate veya başka decision artifact'i değiştirmek.
- Master PDF ile `plan/PDF_SOURCE.md` yalnız immutable historical trace girdisidir;
  current source packet, named approval veya remediation authority yerine geçmez.
- Kanıt gelmeden business, legal, provider veya teknik sonuç varsaymak.

## Dependencies

- V0-GOV-035
## Onay

Approved by Semih — Founder/Product Owner — 2026-08-15. Karar geçerlidir,
yeni provenance paketi beklenmez.

## Deliverables

- `docs/decision-revalidation/V0-REV-002-v0-dat-002-decision-supplement.md` yolunda tek additive decision supplement.
- Kaynak snapshot/hash'i, approval transcript'i ve doğrulama çıktıları.

## Acceptance evidence

- Blocker'daki özel kanıt açığı gerçek kaynak ve named approval ile kapanır.
- Supplement seçilen sonuç, reddedilen alternatifler, gerekçe, erişim tarihleri
  ve etkilenen exact Task ID'leri içerir.
- `docs/data/canonical-value-catalog.md` ve `V0-DAT-002` dosyalarının before/after hash'i aynıdır.
- Supplement dışında yeni decision record sayısı `0` olur.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0`
  verir; kanıtlar yalnız `evidence/V0-REV-002/**` altındadır.

## Handoff

- V0-GOV-036
