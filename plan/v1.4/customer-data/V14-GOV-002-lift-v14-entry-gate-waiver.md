# V14-GOV-002 - Lift GATE-V14-ENTRY's dependency on GATE-V13-EXIT

- Task ID: V14-GOV-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-22

## Goal

`V14-GOV-001` (2026-09-18) taslak/standalone rotasını, `GATE-V14-ENTRY`nin
`GATE-V13-EXIT`e bağımlılığını Semih'in kendisinin koyduğu bilinçli bir
SIRALAMA kararı olduğunu belirterek seçmişti. Semih 2026-09-22'de bu kararı
("Gate kural ben koydum, şimdi de kaldırıyorum") açıkça geri aldı — v1.2
(Yemeksepeti kanalı) ve v1.4 (müşteri hesabı/faturalama) zincirlerinin dış
bağımlılık gerektirmeyen tüm kodlama işinin artık gerçek Owned surface'ta
(taslak/standalone değil) tamamlanması kararının bir parçası.

`GATE-V14-ENTRY`nin fiili mekanik uygulaması `tools/task-scope/
task_scope_tool.py`'nin `check_entry_gate` fonksiyonu: bir V14 görevi için
V13'ün TÜM görevlerinin `Done`/`NotApplicable` olmasını şart koşuyordu (V0
deferral mekanizmasının aynısı, ama V0-EXIT dışındaki gate'ler için istisna
yoktu). v1.3'ün 14 görevi (`V13-FSC-001..004`, `V13-HUG-001..004`,
`V13-MCD-001..004`, `V13-ALC-004`, `V13-PUI-003`) gerçek dış sözleşme
kanıtı (Token/Beko, meal-card, QNB fiscal strateji) olmadan `Done` olamaz —
kendi Acceptance evidence metinleri onaylı sağlayıcı yoksa schema/stub
üretilmesini bile açıkça yasaklıyor. Bu 14 görev sonsuza kadar `GATE-V13-
EXIT`i (dolayısıyla `GATE-V14-ENTRY`yi) kapalı tutacaktı.

Ayrıca bu incelemede `_DEFERRED_TASK_RECORDS`'un (V0 deferral mekanizması)
`plan/GATES.md`'nin gerçek `V0_DEFERRED_TASKS` tablosuyla ARTIK EŞLEŞMEDİĞİ
bulundu — `reopen_stage` ve `required_evidence` alanları en az 6 satırda
farklıydı (muhtemelen `V0-GOV-064`/C98'in Hugin→Token retarget'i ve diğer
tarihsel GATES.md güncellemeleri sırasında bu ikinci kopyanın senkronize
edilmemesinden). Gerçek `parse_v0_deferral_ids(Path('plan'))` çağrısı bu
yüzden HER ZAMAN `TaskParseError` fırlatıyordu — `check_entry_gate`,
GATE-V0-EXIT'i her V1+ görevi için "deferral table rejected" ile
reddediyordu (testler yalnız kendi fixture'larını kullandığı için bunu
hiç yakalamamıştı). Bu, benim yeni eklediğim V13 waiver mekanizmasının da
aynı şekilde bozuk olacağı anlamına geliyordu, o yüzden aynı oturumda
düzeltildi.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/v1.4/customer-data/V14-GOV-002-lift-v14-entry-gate-waiver.md`
- `evidence/V14-GOV-002/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope.py
  (V1-GOV-066 sahipliğinde) — yalnız yeni `TestV13ExitEntryWaiver` sınıfı
  ve `DEFERRED_ROWS`'un `plan/GATES.md`'nin gerçek içeriğiyle eşleşecek
  düzeltmesi eklendi; mevcut testler değişmedi.

## In scope

1. `plan/GATES.md`'ye V0 deferral tablosuyla birebir aynı fail-closed
   desende yeni bir işaretli tablo (`V13_EXIT_ENTRY_WAIVER`): yukarıdaki 14
   V13 görevi, 2026-09-22 onay tarihiyle.
2. `tools/task-scope/task_scope_tool.py`: `parse_v13_exit_waiver_ids` (V0'ın
   `parse_v0_deferral_ids`'ının birebir aynısı — marker/header/separator/
   satır regex/tam-eşleşme kontrolü) + `check_entry_gate`'e yeni
   `gate_id == "GATE-V13-EXIT"` dalı: yalnız waiver tablosundaki 14 görev
   dışında kalan V13 görevleri `still_open` sayılır.
3. **Bulunan gerçek kusur düzeltmesi:** `_DEFERRED_TASK_RECORDS`'un
   `reopen_stage`/`required_evidence` alanları `plan/GATES.md`'nin gerçek
   `V0_DEFERRED_TASKS` tablosuyla eşleşecek şekilde güncellendi (6 satır).
4. `tests/Architecture/TaskScope/test_task_scope.py`: `DEFERRED_ROWS`
   sabiti de aynı düzeltmeyle senkronize edildi (aksi hâlde test fixture'ı
   gerçek dosyadan farklı bir "doğru" kabul ediyordu); yeni
   `TestV13ExitEntryWaiver` sınıfı (4 test: waived set gate'i kapatır,
   waived olmayan bir V13 görevi gate'i açık tutar, tablo eksikse
   fail-closed, waiver başka gate'i etkilemez) V0'ın kendi
   `TestDeferredV0EntryGate` desenini birebir izliyor.
5. `plan/TRACEABILITY.md`'ye `C100` kaydı.

## Out of scope

- v1.3'ün kendisinin 14 görevini `Done`/`NotApplicable` yapmak — onlar
  gerçek dış sözleşme kanıtı olmadan hâlâ kapanamaz, bu görev yalnız V14
  giriş sırasını değiştiriyor.
- v1.2'nin kendi `V0-YSP-001`'e olan per-task `Dependencies` referansı —
  bu bir `GATE-*` değil, `TASK_STANDARD.md:57-60`'ın standart dependency
  kuralı (yalnız `Done`-işaretleme'yi engeller, `InProgress` almayı değil);
  ayrı bir mekanizma değişikliği gerekmiyor.
- `GATE-V13-EXIT`'in kendisi — yalnız 14 görev gerçek kanıtla kapanınca
  fiilen kapanır; bu waiver yalnız `GATE-V14-ENTRY` türetimini etkiler.

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-22. "Gate kural ben
koydum, şimdi de kaldırıyorum" — `V14-GOV-001`'in 2026-09-18 tarihli
sıralama kararını bilerek geri aldı.

## Deliverables

- `tools/task-scope/task_scope_tool.py`: `parse_v13_exit_waiver_ids`,
  `_V13_EXIT_WAIVER_RECORDS`, `check_entry_gate`'in yeni dalı, `_DEFERRED_
  TASK_RECORDS` düzeltmesi.
- `tests/Architecture/TaskScope/test_task_scope.py`: `TestV13ExitEntryWaiver`
  (4 test), `DEFERRED_ROWS` düzeltmesi.
- `plan/GATES.md`: `V13_EXIT_ENTRY_WAIVER` tablosu + `GATE-V14-ENTRY` satır
  notu.
- `plan/TRACEABILITY.md`: `C100`.

## Acceptance evidence

- `python -m pytest tests/Architecture/TaskScope/ -q` → 137/137 (106 önceki
  + 4 yeni `TestV13ExitEntryWaiver` + `test_task_scope_markdown_boundary.py`
  27), 0 başarısız.
- Gerçek `plan/` dizinine karşı `parse_v0_deferral_ids`/
  `parse_v13_exit_waiver_ids` çağrıları hatasız dönüyor (önceden V0'ınki
  her zaman `TaskParseError` fırlatıyordu — düzeltildi).
- `check_entry_gate` gerçek `V14-CST-001` görevine karşı çalıştırıldığında
  artık yalnız v1.3'ün gerçekten açık, dış-bağımlılık-DIŞI 8 görevini
  (`V13-PAY-003/004/005`, `V13-PUI-001/004`, `V13-REC-001`, `V13-RPT-001`,
  `V13-TBL-001`) listeliyor — 14 waived görev artık engel değil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V14-CST-001
