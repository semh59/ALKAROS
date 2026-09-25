# V12-GOV-004 - Waive the per-task V0-YSP-001 dependency for six Faz 3 Yemeksepeti channel tasks

- Task ID: V12-GOV-004
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`V0-YSP-001` (Yemeksepeti Partner API v2.0.2 sözleşme doğrulaması) gerçek Partner
Portal credential'ı, sandbox erişimi ve imzalı/tokenlı gerçek webhook transcript'i
olmadığı için `Blocked`. Faz 3'ün 12 v1.2 görevinden altısı (`V12-STK-001`,
`V12-MAP-001`, `V12-MAP-002`, `V12-ONL-001`, `V12-ONL-003`, `V12-ONL-004`) bu
göreve kendi `## Dependencies` bölümünde doğrudan bağlı; `V12-QRO-003`,
`V12-ONL-002`, `V12-ONL-005`, `V12-OUI-001`, `V12-REC-001` ve `V12-RPT-001`
bunlara zincirli. `plan_audit_tool.py`'nin `DONE_DEPENDENCY_NOT_FINAL` kontrolü ve
`task_scope_tool.py`'nin dependency kontrolü bu 12 görevin hiçbirinin `Done`
olmasına izin vermiyordu — `V12-QRO-003` (QR onayı) gibi Yemeksepeti'ye hiç
dokunmayan bir görev bile `V12-STK-001` üzerinden kilitliydi.

Semih 2026-09-25'te, Faz 2'deki `V13-GOV-008` emsaliyle, bu altı doğrudan kenarın
resmi olarak esnetilmesini istedi. Her tüketici kendi Owned surface'ında
kanal-bağımsız bir port ve gerçek domain davranışı yazar; Yemeksepeti'ye özgü
HTTP/webhook kodu yalnızca herkese açık Partner API v2.0.2 belgesine dayanan,
açıkça **doğrulanmamış taslak** olarak işaretlenen koddur.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/v1.2/governance/V12-GOV-004-yemeksepeti-channel-dependency-waiver.md`
- `evidence/V12-GOV-004/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope.py
  (V1-GOV-066 sahipliğinde) — yalnız yeni `TestYemeksepetiChannelDependencyWaiver`
  sınıfı eklendi; mevcut testler değişmedi.

## In scope

1. `tools/plan-audit/plan_audit_tool.py`: `YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER`
   (tam 6 `(consumer, V0-YSP-001)` çifti), `find_non_final_ancestors` istisnası ve
   `plan/GATES.md` işaretli tablosuyla fail-closed çapraz doğrulama
   (`GATES_YEMEKSEPETI_CHANNEL_WAIVER_MARKER_MISSING`/`_MISMATCH`).
2. `tools/task-scope/task_scope_tool.py`: birebir aynı 6 çift
   (`_YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER`) ve `validate_task_metadata` istisnası.
3. `plan/GATES.md`: `V12_YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER` işaretli tablosu.
4. `tests/Architecture/TaskScope/test_task_scope.py`: 3 test (waived kenar
   engellemez, aynı görevin başka bağımlılığı zorunlu kalır, waiver yalnız kayıtlı
   tüketiciye özgüdür).
5. `plan/TRACEABILITY.md`: `C103` kaydı.

## Out of scope

- `V0-YSP-001`'i veya `V20-INT-003`'ü kapatmak ya da metnini değiştirmek.
- Tüketici görevlerin "gerçek sandbox kanıtı" isteyen maddelerini karşılanmış saymak:
  bu kanıt `V0-YSP-001` ve `V20-INT-003` sahipliğinde açık kalır.
- Üretimde bir Yemeksepeti kanalını etkinleştirmek veya herhangi bir kodu
  "doğrulandı" diye nitelemek.
- 12 Faz 3 görevinin kodunu yazmak; her biri kendi Task ID'siyle ayrı ilerler.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-25. Faz 3 oturum talimatı:
"V0-YSP-001 Blocked (gerçek partner API erişimi yok). Faz 2'deki V13-GOV-008 gibi
bir bağımlılık feragati görevi aç, sonra V12-MAP-001/002, V12-ONL-001..005'i
port/adaptör olarak yaz; HTTP kısmı 'doğrulanmamış taslak' işaretli olsun.
Hiçbir şeyi 'doğrulandı' diye yazma."

## Deliverables

- `tools/plan-audit/plan_audit_tool.py`: `YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER`,
  `find_non_final_ancestors` istisnası, GATES.md çapraz doğrulaması.
- `tools/task-scope/task_scope_tool.py`: `_YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER`.
- `plan/GATES.md`: `V12_YEMEKSEPETI_CHANNEL_DEPENDENCY_WAIVER` tablosu.
- `tests/Architecture/TaskScope/test_task_scope.py`: `TestYemeksepetiChannelDependencyWaiver`.
- `plan/TRACEABILITY.md`: `C103`.

## Acceptance evidence

- `python -m pytest tests/Architecture/TaskScope/ -q` → 143 passed (140 önceki + 3 yeni).
- Mutasyon: `task_scope_tool.py`'deki istisna devre dışı bırakıldığında yeni sınıfın
  2 testi kırmızıya döndü; geri alındıktan sonra yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı; GATES.md
  tablosunun bir satırı silindiğinde `GATES_YEMEKSEPETI_CHANNEL_WAIVER_MISMATCH` üretildi.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Kanıt: `evidence/V12-GOV-004/completion-evidence.txt`.

## Handoff

- V12-STK-001
- V12-MAP-001
- V12-MAP-002
- V12-ONL-001
- V12-ONL-003
- V12-ONL-004
