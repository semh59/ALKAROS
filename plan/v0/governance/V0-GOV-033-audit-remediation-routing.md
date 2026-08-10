# V0-GOV-033 - Route the frozen independent-audit findings

- Task ID: V0-GOV-033
- Status: Done
- Assignee: 019fea95-a9d0-78a1-887c-5544a4d1b19f
- Work type: documentation
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

SHA-256 ile dondurulmuş 2026-08-10 bağımsız denetimindeki 42 bulguyu,
tek-sorumluluklu yeni görev kimliklerine eksiksiz yönlendirmek; PDF'yi bu
remediasyonun kaynağı gibi göstermemek ve candidate bulgulara kanıtsız üretim
yazma yetkisi vermemek.

## Owned surface

- `plan/v0/governance/V0-GOV-033-audit-remediation-routing.md`
- `plan/v0/governance/V0-GOV-034-remediation-task-materialization.md`
- `plan/v0/governance/V0-GOV-035-remediation-admission-control.md`
- `plan/v0/governance/V0-GOV-032-deferral-entry-gate-recognition.md` (yalnız
  tests/Architecture/TaskScope/test_task_scope.py ownership daraltması)
- `plan/TRACEABILITY.md`
- `plan/AUDIT_REMEDIATION_ROUTING.csv`
- `plan/AUDIT_REMEDIATION_ROUTING.json`
- `evidence/V0-GOV-033/**`

## In scope

- Denetim register dosyasının SHA-256 ve 42 satır sayısını doğrulamak.
- Her bulguyu bir veya daha fazla kesin owner görevine ve dependency sırasına
  bağlamak; ortak dosya yüzeylerini seri yürütmek.
- Tarihsel bulguları history rewrite yerine immutable attestation ve güncel
  re-acceptance görevlerine yönlendirmek.
- `PARTIAL`, `CANDIDATE` ve `UNPROVEN` kararları üretim düzeltmesinden önce
  yeniden üretim kapısına bağlamak.
- Sonraki görev materyalizasyonu ile fail-closed admission görevini planlamak.

## Out of scope

- Ürün, test, migration, CI, gate veya validator davranışını değiştirmek.
- Mevcut `Done` görevleri yeniden açmak veya `V0-GOV-032` içindeki exact
  `test_task_scope.py` sahiplik daraltması dışında gövdelerini değiştirmek.
- Kullanıcıya ait dirty çalışma ağacını silmek, taşımak, resetlemek veya örtük
  biçimde temiz remediation dalına kopyalamak.
- PDF coverage üretmek, eski commitleri yeniden yazmak, force-push yapmak veya
  herhangi bir bulguyu bu routing göreviyle kapanmış saymak.

## Dependencies

- V0-GOV-017
- V0-GOV-028
- V0-GOV-032

## Deliverables

- `finding_id` bazında 42/42 kapsama sahip, CSV ve JSON biçimleri birebir aynı
  routing ledger'ı.
- Yeni görev kimlikleri, sıralı execution lane, dependency, kapanış kanıtı ve
  model/effort seçimini taşıyan sabit şema.
- Child görev dosyalarını exact path ile üretecek `V0-GOV-034` ve yalnız
  kayıtlı remediasyon kimliklerini kabul edecek `V0-GOV-035` planları.
- `test_task_scope.py` için `V0-GOV-032` sahipliğinin C52 ile daraltılması ve
  `V0-GOV-035` görevine tek-sahipli devir kaydı.
- PDF yerine `CORR:C52` ve dondurulmuş denetim hash'ini yetkili kaynak yapan
  traceability kaydı.

## Acceptance evidence

- `finding-register.csv` SHA-256 değeri
  `EE46C13075AFA4F7B7CECF02E6899DB9AF66FDB252C7052841BDB0BC49034F7F`
  ve satır sayısı `42` olarak doğrulanır.
- CSV ile JSON aynı 42 benzersiz `finding_id` değerini ve aynı owner görevlerini
  taşır; eksik ve duplicate bulgu sayısı `0` olur.
- Yeni görev kimlikleri mevcut 268 görevle çakışmaz ve dependency yönlendirmesi
  döngü üretmez.
- `uv run --python 3.12.12 --with-requirements plan/validation-requirements.lock
  python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- `uv run --python 3.12.12 --with-requirements plan/validation-requirements.lock
  python -B tools/plan-audit/plan_audit_tool.py validate-coverage` exit code `0`
  verir.
- Başlangıçta doğrulanan manifest ve Markdown lint arızaları bu görevde
  gizlenmez; sırasıyla `VER-GOV-003` ve `VER-GOV-002` owner görevlerine bağlı
  transcript ile kayıt altına alınır.
- Komutlar, exit code'lar ve hash'ler `evidence/V0-GOV-033/**` altında kayıtlıdır.

## Handoff

- V0-GOV-034
