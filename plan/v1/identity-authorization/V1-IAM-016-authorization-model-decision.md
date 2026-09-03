# V1-IAM-016 - Authorization model decision

- Task ID: V1-IAM-016
- Status: Planned
- Assignee: Unassigned
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:II.2.1
- PDF:III.3
- PO:2026-09-04

## Goal

`pos.cashier.mutate` tek kaba iznini yerine koyacak yetkilendirme modelini karar
altına almak: granüler izin sözlüğü, gerçek `waiter` rolü, politika motoru,
asenkron bağlamsal yetki akışı, süreli devir, sınırlı çevrimdışı yetki bütçesi ve
davranışsal sıkılaştırma. Rakip POS'ların senkron yönetici PIN deseni açıkça
reddedilir.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-016-authorization-model-decision.md`
- `docs/domain/authorization-model.md`
- `evidence/V1-IAM-016/**`
- `docs/domain/authorization-model.md` yeni dosyadır; sahibi bu görevdir.
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-002
- V1-RMD-097

## Deliverables

- `docs/domain/authorization-model.md` karar kaydı: kaynaklar, erişim tarihi,
  onaylayan, seçilen sonuç, reddedilen alternatif olarak Toast/Square/Lightspeed
  senkron PIN deseni ve etkilenen kesin task kimlikleri. İzin sözlüğü, rol
  matrisi ve invariant listesi aynı kaydın ekidir.

## Acceptance evidence

- `docs/domain/authorization-model.md` karar kaydı biçimine uyar ve
  `markdownlint-cli2` kök yapılandırma glob'larında temizdir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` ile `verify-manifest`
  sıfır hata verir.
- Semih dokümanı okur ve `Approver` satırını doldurur; rol matrisindeki üç ürün
  kararının 3. bölümdeki karar notlarıyla örtüştüğünü doğrular.

## Handoff

- V1-IAM-017
