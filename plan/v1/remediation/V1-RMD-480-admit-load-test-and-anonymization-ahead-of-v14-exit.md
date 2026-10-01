# V1-RMD-480 - V15-PER-001 ve V15-KVK-002'yi GATE-V14-EXIT kapanmadan tamamlanabilir kılmak

- Task ID: V1-RMD-480
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`V15-PER-001` (kritik yol yük testi) çalıştırıldı ve hedefleri karşıladı; `V15-KVK-002` (KVKK anonimleştirme) kodlanmaya hazır. İkisi de dış bağımlılık
gerektirmez, ancak kapanış kapısı v1.5 görevinin `GATE-V14-EXIT` kapanana kadar tamamlanmasını reddeder (v1.4'ün 14 açık görevi, çoğu QNB ve dış sözleşmeye bağlı).
Semih 2026-10-01'de bu iki görev için istisna verdi. `V1-RMD-461`in kabul tablosuna (`V14_EXIT_AHEAD_ADMISSION`) iki kayıt eklenir; tablo araçtaki sabit kayıtlarla
BİREBİR eşleşmeye devam eder. Kural diğer bütün v1.5 görevleri için aynen kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-480-admit-load-test-and-anonymization-ahead-of-v14-exit.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/task-scope/task_scope_tool.py - yalnız kabul kayıt kümesine iki kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/GATES.md - yalnız kabul tablosuna iki satır ve açıklama
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/TRACEABILITY.md - yalnız yeni izlenebilirlik kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope_v14_exit_admission.py - yalnız beklenen kayıt kümesi ve testler

## In scope

- `V15-PER-001` ve `V15-KVK-002` için kabul kaydı; tablo, sabit küme ve testler birebir eşleşir; fazladan ya da değiştirilmiş kayıtta kapı kapalı kalır.

## Out of scope

- v1.4 görevlerinin kendisi ve diğer v1.5 görevleri için istisna.

## Dependencies

- V1-RMD-461

## Acceptance evidence

- `tests/Architecture/TaskScope` takımı exit code 0; çıktılar `evidence/V1-RMD-480/` altındadır.

## Handoff

- None
