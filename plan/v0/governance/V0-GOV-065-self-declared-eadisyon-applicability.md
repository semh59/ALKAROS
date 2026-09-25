# V0-GOV-065 - Replace centralized e-Adisyon applicability determination with self-declared configuration

- Task ID: V0-GOV-065
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: plan-change
- Surface state: Existing

## Source basis

- CORR:C99
- EXT:GIB-VUK509-2026
- EXT:GIB-EADISYON
- EXT:GIB-YNOKC-GUIDE
- EXT:GIB-YNOKC-SSS
- PO:2026-09-18

## Goal

`V0-CMP-001`'in kendi Blocker'ının istediği "mali müşavir onaylı, hedef
restoran profili için tek bir uygulanabilirlik kararı"nı, Semih'in açık
kararıyla, **restoranın kendi beyanına dayalı, GİB'in gerçek yayımlanmış
koşuluna birebir eşlenen bir kuruluş-zamanı yapılandırmayla** değiştirmek:
ALKAROS hangi işletmenin yükümlü olduğuna karar vermez; her işletme,
kendi muhasebecisiyle doğruladığı 3 gerçek olguyu (masada servis mi,
gerçek usulde mi vergilendiriliyor, e-Fatura/e-Arşiv mükellefi mi) kurulum
ekranında beyan eder, ALKAROS bu beyana göre zaten GİB'in kendi yayımlanmış
kuralını (VUK 509 IV.12) uygular.

## Owned surface

- `plan/v0/compliance/V0-CMP-001-gib-eadisyon-scope.md`
- `evidence/v0/compliance/V0-CMP-001/**`
- `evidence/V0-GOV-065/**`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/TaskScope/test_task_scope.py
  (V1-GOV-066 sahipliğinde kalır) — yalnız `DEFERRED_TASK_IDS`/`DEFERRED_ROWS`
  kümelerinden `V0-CMP-001` satırı çıkarılır, test dosyasının başka hiçbir
  bölümü değişmez.

## In scope

- `V0-CMP-001`'in Acceptance evidence'ını, "hedef profil için tek karar"
  yerine "3 gerçek, kaynaklı beyan sorusu + her kombinasyon için doğru
  davranış eşlemesi" olacak şekilde daraltmak.
- `plan/GATES.md`'nin `V0_DEFERRED_TASKS` tablosundan `V0-CMP-001` satırını
  çıkarmak (C73'ün `V0-BKP-001`/`V0-BKP-002` emsali) ve bunu
  `tools/plan-audit/plan_audit_tool.py`'nin `V0_DEFERRED_TASKS` kümesi ile
  `tools/task-scope/task_scope_tool.py`'nin `_DEFERRED_TASK_RECORDS`
  kümesinde eşzamanlı yapmak.
- `GATE-V13-FSC-STRATEGY`'nin kapanma koşulunu güncellemek: artık
  "V0-CMP-001'in TEK bir strateji seçmesi" değil, "V0-CMP-001'in beyan
  matrisini yayımlamış olması + V13-FSC-004/005'in her ikisinin de kendi
  gerçek kanıtıyla Done veya NotApplicable olması" (beyan `Evet` ise
  Token/Beko branch aktif olur — bkz. `V13-FSC-005`'in zaten `NotApplicable`
  kararı; beyan `Hayır` ise hiçbir branch aktive edilmez).

## Out of scope

- Token/Beko'nun ürettiği basket-kapanış belgesinin VUK 509 IV.12'nin
  e-Adisyon şeklini hukuken tam karşıladığını iddia etmek — bu, `V20-CMP-001`
  (nihai compliance sign-off) kapsamındaki AYRI bir sorudur ve bu görevle
  KAPANMAZ; üretim/gerçek para ile çalışma öncesi hâlâ oradan geçmesi
  gerekir (`plan/GATES.md`'nin "Canlı veri kuralı"ndan bağımsız olarak).
- Yeni bir kurulum-ekranı task'ı açmak veya kodlamak (bu, ayrı bir
  `implementation` görevi; bu görev yalnız plan/gate değişikliğidir).
- Vergi hukuku yorumu üretmek (Out of scope zaten `V0-CMP-001`'in kendi
  dosyasında da yasaklı).

## Dependencies

- None

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-18. Karar akışı: (1)
Semih'e `V0-CMP-001`'in Blocker'ı (mali müşavir onayı gerektiği)
açıklandı, (2) Semih "basit arayüzde tebliğ olduğu düzeni seçtiririz,
biter" diyerek self-declared configuration çözümünü önerdi, (3) gerçek
GİB kaynağı (VUK 509 IV.12, zaten `evidence/v0/compliance/V0-CMP-001/
gib-applicability-matrix.md`'de kayıtlı) kontrol edildi ve önerinin bu
kaynağın kendi yayımladığı 3 boolean koşula (masada servis + gerçek usul

- e-Fatura/e-Arşiv mükellefiyeti) birebir eşlendiği doğrulandı, (4) önerinin
`V20-CMP-001`'in kendi ayrı sorusunu (Token/Beko belgesinin hukuken yeterli
olup olmadığı) KAPATMADIĞI açıkça belirtildi ve Semih bu ayrımla devam
onayı verdi. Gerekçe: her restoranın kendi vergi/hizmet profilini
ALKAROS'tan daha iyi bildiği (ve zaten kendi muhasebecisiyle doğrulaması
gerektiği) prensibi — bu, gerçek muhasebe yazılımı pazarında (Logo, Mikro,
Paraşüt) zaten kullanılan, kanıtlanmış bir desendir; ALKAROS'un işi doğru
davranışı GERÇEK GİB metnine göre uygulamak, işletmenin kendi yükümlülüğünü
belirlemek değildir.

## Deliverables

- `evidence/v0/compliance/V0-CMP-001/gib-applicability-matrix.md` — beyan
  matrisi (3 soru + 2^3 kombinasyonun her biri için doğru davranış) ile
  genişletildi.
- `plan/v0/compliance/V0-CMP-001-gib-eadisyon-scope.md` — Blocked'tan
  çıkarıldı, Acceptance evidence rescoped, `## Onay` bloğu eklendi.
- `plan/GATES.md` — `V0_DEFERRED_TASKS` tablosundan `V0-CMP-001` satırı
  çıkarıldı; `GATE-V13-FSC-STRATEGY` açıklaması güncellendi.
- `plan/TRACEABILITY.md` — `C99` kaydı.
- `tools/plan-audit/plan_audit_tool.py`, `tools/task-scope/task_scope_tool.py`,
  `tests/Architecture/TaskScope/test_task_scope.py` — deferred küme/satır
  senkronizasyonu (C73 emsali).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `pytest tests/Architecture/TaskScope/test_task_scope.py` → yeşil (deferred
  satır kümesi güncel).
- `git diff` ile doğrulanabilir: `plan/PDF_COVERAGE.md` ve
  `plan/AUDIT_REPORT.md`'de hiçbir satır değişmedi.

## Handoff

- V0-CMP-001
- V13-FSC-003
