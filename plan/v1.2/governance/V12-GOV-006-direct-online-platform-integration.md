# V12-GOV-006 - Online yemek platformlarına doğrudan entegrasyon kararı ve görev planı

- Task ID: V12-GOV-006
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Yemeksepeti dışındaki online yemek platformlarının ALKAROS'a nasıl bağlanacağına karar vermek ve kararı
uygulanabilir görevlere bölmek. Semih 2026-09-26'da "Doğrudan entegre edeceğiz" dedi: aracı firma (tek REST
köprüsü) kullanılmaz, her platform kendi resmî arayüzüyle bağlanır.

## Owned surface

- `plan/v1.2/governance/V12-GOV-006-direct-online-platform-integration.md`
- `evidence/V12-GOV-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - plan/OFFICIAL_SOURCE_REGISTER.md — `TGO-MEAL-API` kaynak satırı.
  - plan/TRACEABILITY.md — `C105` plan değişikliği kaydı.
  - plan/v1.2/README.md — yeni modüllerin listesi.
  - plan/v1.2/online-ordering/V12-ONL-006-online-order-provider.md
  - plan/v1.2/online-ordering/V12-ONL-007-online-provider-adapter-contract.md
  - plan/v1.2/online-ordering/V12-ONL-008-provider-neutral-inbox-and-mapping.md
  - plan/v1.2/online-ordering/V12-ONL-009-provider-order-polling.md
  - plan/v1.2/reconciliation/V12-REC-002-provider-aware-reconciliation.md
  - plan/v1.2/online-operations-ui/V12-OUI-002-provider-label-and-credentials.md
  - plan/v1.2/trendyol-go/V12-TGO-001-meal-api-contract.md
  - plan/v1.2/trendyol-go/V12-TGO-002-order-intake.md
  - plan/v1.2/trendyol-go/V12-TGO-003-outbound-status.md
  - plan/v1.2/trendyol-go/V12-TGO-004-menu-availability-and-price.md
  - plan/v1.2/trendyol-go/V12-TGO-005-uber-eats-transition-model.md
  - plan/v1.2/migros-yemek/V12-MGY-001-pos-integrator-contract.md
  - plan/v1.2/migros-yemek/V12-MGY-002-order-adapter.md

## In scope

1. Karar kaydı (`evidence/V12-GOV-006/decision-record.md`): pazar durumu, her platformun erişim yolu,
   kaynaklar ve erişim tarihleri, seçilen sonuç, reddedilen alternatifler, etkilenen görev kimlikleri.
2. Yukarıdaki 13 görev dosyası. Hepsi `Planned`; dış sözleşme görevleri (`V12-TGO-001`, `V12-MGY-001`) `Blocked`.
3. Trendyol Go resmî geliştirici belgesinin kaynak defterine eklenmesi ve `C105` kaydı.

## Out of scope

- Kod; her görev kendi Task ID'si ile ayrı uygulanır.
- Getir Yemek: platform Uber Eats Trendyol Go'ya devredildiği için ayrı görev açılmaz.
- Aracı entegrasyon firmaları (reddedilen alternatif).
- Görevlerin sırası ve Faz 4 ile ilişkisi (Semih'in ayrı kararı).

## Dependencies

- V12-GOV-005

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-26: "Doğrudan entegre edeceğiz".

## Deliverables

- Karar kaydı ve 13 görev dosyası.

## Acceptance evidence

- `plan_audit_tool.py validate` 0 hata / 0 uyarı; `consistency_audit.py` temiz.
- `task_scope_tool.py --task-id V12-GOV-006 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
