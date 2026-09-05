# V1-GOV-058 - Master custody reopen and remediation wave 18

- Task ID: V1-GOV-058
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Aylarca çalışan bir lokantada `idempotency_keys` (her yazımda bir BYTEA yanıt zarfı, V0-ARC-003 gereği 24 saat saklama) ve `identity.device_sessions` (süresi dolmuş ve iptal edilmiş oturumlar, `session_operations` çocuk kayıtlarıyla birlikte) tabloları sınırsız büyüyor; `IdempotencyKeyStore.SweepExpiredAsync` mevcut ama hiç çağrılmıyor, cihaz oturumları için hiç temizlik yok ve Host'ta periyodik bakım altyapısı yok. Bu operasyonel veritabanı hijyeni eksikliğini kapatmak için `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-091`) ve kapanış görevi (`V1-GOV-059`) planlanır. KVKK kişisel veri saklama/anonimleştirme kapsam dışıdır; `V0-CMP-003` envanteri ve `V15-KVK-001`/`V15-KVK-002` görevlerine aittir (saklama süreleri 5-10 yıl).

## Owned surface

- `plan/v1/governance/V1-GOV-058-master-custody-reopen-and-remediation-wave-18.md`
- `plan/v1/remediation/V1-RMD-091-operational-data-housekeeping-sweep.md`
- `plan/v1/governance/V1-GOV-059-wave18-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 18. dalga görevleriyle (`V1-RMD-091`, `V1-GOV-059`) genişletmek ve sayımı güncellemek.
- Yüzey devri: `src/Host/Program.cs` yüzeyi `V1-RMD-046`'dan `V1-RMD-091`'e devredilir.
- `EXT:POSTGRESQL-18.4` tüketici listesine `plan/OFFICIAL_SOURCE_REGISTER.md` içinde `V1-RMD-091` eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- KVKK kişisel veri saklama, silme ve anonimleştirme; `V15-KVK-001`/`V15-KVK-002` kapsamındadır.
- `orders`, `bills`, fiscal ve invoice gibi yasal saklama süresi olan iş kayıtları.
- `table_reservations` ve provider payload temizliği; ayrı değerlendirme.

## Dependencies

- V1-GOV-057

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-091`) ve kapanış görevi (`V1-GOV-059`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-091
