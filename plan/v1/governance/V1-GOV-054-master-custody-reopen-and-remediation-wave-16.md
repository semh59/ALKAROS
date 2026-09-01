# V1-GOV-054 - Master custody reopen and remediation wave 16

- Task ID: V1-GOV-054
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

1M sipariş satırı ölçek probu, `orders.orders` üzerinde iki sorgu deseninin tam tablo seq scan yaptığını ölçmüştür: tarih aralıklı raporlama (bir iş günü cirosu 134 ms) ve masaya ait güncel açık sipariş (158 ms). Her ikisi de indeks eksikliğinden kaynaklanır ve ölçülen kanıtla çözülür. `orders` ölçek indeks migration'ı için `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-089`) ve kapanış görevi (`V1-GOV-055`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-054-master-custody-reopen-and-remediation-wave-16.md`
- `plan/v1/remediation/V1-RMD-089-orders-scale-index-migration.md`
- `plan/v1/governance/V1-GOV-055-wave16-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`
- `evidence/V1-GOV-054/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 16. dalga görevleriyle (`V1-RMD-089`, `V1-GOV-055`) genişletmek ve sayımı güncellemek.
- Yüzey devirleri: `database/MigrationComposition/order.json` ve `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` yüzeyleri `V1-GOV-040`'tan `V1-RMD-089`'a devredilir; böylece migration composition 16. dalgada tek görevde güncellenir.
- `EXT:POSTGRESQL-18.4` tüketici listesine `plan/OFFICIAL_SOURCE_REGISTER.md` içinde `V1-RMD-089` eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- `V15-PER-001` kritik yol yük testlerinin tam kapsamı; `GATE-V14-EXIT` bağımlıdır.
- `kitchen_tickets` ve `audit_events` indeksleri; ölçek probunda bu tablolarda indeks açığı bulunmamıştır (`ix_kitchen_tickets_station_status`, `ix_audit_events_aggregate` ve `ix_audit_events_occurred_at` mevcuttur).
- Veri saklama ve purge işleri; bunlar sonraki dalgaya aittir.

## Dependencies

- V1-GOV-053

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-089`) ve kapanış görevi (`V1-GOV-055`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-089
