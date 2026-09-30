# V1-RMD-461 - V15-KVK-001'i GATE-V14-EXIT kapanmadan tamamlanabilir kılmak

- Task ID: V1-RMD-461
- Status: Planned
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

`V15-KVK-001` (KVKK saklama yürütmesi) kodlanmış ve test edilmiştir, ancak kapanış kapısı v1.5 görevinin `Done` olmasını
`GATE-V14-EXIT` kapanana kadar (v1.4'ün 14 açık görevi, çoğu QNB ve dış sözleşmeye bağlı) reddeder. Semih 2026-09-30'da bu
görev için istisna verdi. Kapı aracına, `V13_EXIT_ENTRY_WAIVER` ile aynı kalıpta, `GATES.md` içindeki işaretli bir tablodan
okunan ve araçtaki sabit kayıtlarla BİREBİR eşleşmesi gereken bir "GATE-V14-EXIT kapanmadan kabul edilen v1.5 görevleri"
tablosu eklenir; tablo yalnız `V15-KVK-001` içerir. Kural diğer bütün v1.5 görevleri için aynen kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-461-admit-kvkk-retention-ahead-of-v14-exit.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/task-scope/task_scope_tool.py - yalnız yeni kabul tablosunun ayrıştırılması ve giriş kapısında uygulanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/GATES.md - yalnız `GATE-V15-ENTRY` notu ve yeni işaretli tablo
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/TRACEABILITY.md - yalnız yeni izlenebilirlik kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope_v14_exit_admission.py - yeni test dosyası

## In scope

- Kabul tablosu, sabit kayıt kümesi ve `check_entry_gate` içindeki uygulaması; eksik, bozuk ya da fazladan kayıtta kapalı kalma.
- Yalnız `GATE-V14-EXIT` için geçerli olması; başka görev ve başka kapılar etkilenmez.

## Out of scope

- v1.4 görevlerinin kendisi ve diğer v1.5 görevleri için istisna.

## Dependencies

- None

## Acceptance evidence

- Yeni testler ve mevcut `tests/Architecture/TaskScope` takımı exit code 0. Çıktılar `evidence/V1-RMD-461/` altındadır.
- `V15-KVK-001` sonraki adımda `Done` olarak commit edilebilir.

## Handoff

- V15-KVK-001
