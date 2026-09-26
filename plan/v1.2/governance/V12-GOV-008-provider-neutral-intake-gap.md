# V12-GOV-008 - Platformdan bağımsız sipariş alımı boşluğunun planlanması

- Task ID: V12-GOV-008
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

`V12-GOV-006` planı Trendyol Go olaylarının ortak gelen kutusuna yazılmasını (`V12-ONL-009`, `V12-TGO-002`)
öngördü, ama kutudaki bir olayı siparişe çeviren alım servisi (`YemeksepetiOrderIntakeService`) tek platforma
bağlı kaldı ve kutudaki şifreli yükü yalnız Yemeksepeti webhook bileşeni açabiliyor. Hiçbir görev bu ikisini
platformdan bağımsız yapmıyordu; Trendyol Go olayları kutuda kalıp hiç siparişe dönüşmezdi. Ayrıca
`V12-ONL-009` kalıcı imleç istediği halde sahipliğinde migration yolu yoktu. Bu görev iki boşluğu planlar;
Semih'in 2026-09-26 kararlarının ("Şimdi, Faz 4'ten önce"; Trendyol Go "Feragatle taslak olarak yaz")
uygulanması için gereklidir, yeni ürün kararı değildir.

## Owned surface

- `plan/v1.2/governance/V12-GOV-008-provider-neutral-intake-gap.md`
- `plan/v1.2/online-ordering/V12-ONL-010-provider-neutral-intake.md`
- `evidence/V12-GOV-008/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - plan/v1.2/online-ordering/V12-ONL-009-provider-order-polling.md — migration yolları ve V12-ONL-010 bağımlılığı.
  - plan/v1.2/trendyol-go/V12-TGO-002-order-intake.md — V12-ONL-010 bağımlılığı.
  - plan/v1.2/README.md — yeni görevin listesi.
  - plan/TRACEABILITY.md — `C107` kaydı.

## In scope

1. `V12-ONL-010` görev dosyası: ortak gelen kutusu yazıcısı/açıcısı ve kayıtlı her platform için çalışan alım.
2. `V12-ONL-009`: imleç tablosu için migration yolları, host kaydı ve `V12-ONL-010` bağımlılığı.
3. `V12-TGO-002`: `V12-ONL-010` bağımlılığı.

## Out of scope

- Kod; her görev kendi Task ID'si ile uygulanır.

## Dependencies

- V12-GOV-006

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-26: ortak çekirdek "Şimdi, Faz 4'ten önce"; Trendyol Go
"Feragatle taslak olarak yaz". Bu görev o kararların uygulanabilir kalması için eksik görevi açar.

## Deliverables

- `V12-ONL-010` görev dosyası, güncellenmiş `V12-ONL-009` ve `V12-TGO-002`, `C107`.

## Acceptance evidence

- `plan_audit_tool.py validate` 0 hata / 0 uyarı; `consistency_audit.py` temiz.
- `task_scope_tool.py --task-id V12-GOV-008 --diff-base <InProgress commit>` exit 0.

## Handoff

- None
