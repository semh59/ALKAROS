# V1-GOV-052 - Master custody reopen and remediation wave 15

- Task ID: V1-GOV-052
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

 1. dalga yük testi temel ölçümü, veritabanı ve çalışma zamanının ayarlanmamış varsayılan değerlerle çalıştığını ortaya koymuştur (`shared_buffers` 128 MB, `work_mem` 4 MB, `random_page_cost` 4, `jit` açık, .NET GC modu doğrulanmamış). Lokantanın canlıya alınacağı adanmış donanımda bunların üretim seviyesine ayarlanması için `GATE-V1-EXIT` kapısı yeniden açılır. Gerekçeli bir PostgreSQL ayar dosyası, Compose çalışma zamanı ve kaynak sınırları, .NET server GC ve öncesi/sonrası ölçüm ile 1 kurtarma görevi (`V1-RMD-088`) ve kapanış görevi (`V1-GOV-053`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-052-master-custody-reopen-and-remediation-wave-15.md`
- `plan/v1/remediation/V1-RMD-088-deployment-infrastructure-performance-tuning.md`
- `plan/v1/governance/V1-GOV-053-wave15-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/OFFICIAL_SOURCE_REGISTER.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 15. dalga görevleriyle (`V1-RMD-088`, `V1-GOV-053`) genişletmek ve sayımı güncellemek.
- `EXT:POSTGRESQL-18.4` tüketici listesine `plan/OFFICIAL_SOURCE_REGISTER.md` içinde `V1-RMD-088` eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Yazma yolu ve production boyutlu veri altında yük testi; `V15-PER-001` (GATE-V14-EXIT bağımlı) kapsamındadır.
- `src/**` uygulama kodunda değişiklik; ayar yalnızca `deploy/`, `compose.yaml` ve çalışma zamanı bayraklarıyla yapılır.
- Cihaz/tarayıcı matrisi ve KVKK saklama uygulaması; sonraki dalgalara aittir.

## Dependencies

- V1-GOV-051

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-088`) ve kapanış görevi (`V1-GOV-053`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-088
