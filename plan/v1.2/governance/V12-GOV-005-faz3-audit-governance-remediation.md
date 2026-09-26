# V12-GOV-005 - Faz 3 bağımsız denetiminin yönetişim bulgularını kapat

- Task ID: V12-GOV-005
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

2026-09-26 bağımsız Faz 3 denetimi (5 ajan, `1c55a901..df4efdf0`) şu yönetişim bulgularını üretti. Semih
aynı gün "en küçük hata bile kritik" kararıyla hepsinin kapatılmasını istedi:

- G1: 13 Faz 3 görev commit'inin hepsi `task_scope_tool.py` diff modunda FAIL veriyor. Owned surface'e görev
  sürerken ters tırnaklı yeni yollar eklendi, görev dosyasına kanıt yazıldı ve `InProgress` hiç commit'lenmedi.
  Ayrıca "Sınırlı ek" satırları araç tarafından hiç tanınmıyor, yani onaylı yöntem denetlenemiyor.
- G2: `V12-ONL-004` ve `V12-ONL-005` `Done`, ama teslim kalemlerindeki gerçek sandbox kanıtı karşılanmadı.
  `ONL-005` feragatin tüketicisi değil ve GATES metni onu adıyla anmıyor.
- G3: `V12-RPT-001` commit'i (`df4efdf0`) `V12-OUI-001`'in görev dosyasını değiştirdi. Ekran okuyucu maddesi
  `V20-UAT-001`'de görünmüyor.
- G4: Feragatin "üç kaynak fail-closed çapraz doğrulanıyor" iddiası abartılı. `task_scope_tool.py` sabitleri hiçbir
  şeyle karşılaştırılmıyor; aynı açık `V13-GOV-008` ödeme feragatinde de var.

## Owned surface

- `plan/v1.2/governance/V12-GOV-005-faz3-audit-governance-remediation.md`
- `evidence/V12-GOV-005/**`
- `tools/task-scope/task_scope_tool.py`
- `tools/plan-audit/plan_audit_tool.py`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-26 kararı):
  - tests/Architecture/TaskScope/test_task_scope.py (V1-GOV-066 sahipliğinde) — yalnız yeni test sınıfları.
  - plan/GATES.md ve plan/TRACEABILITY.md — `C104` kaydı ve feragat metnine ONL-004/005 sandbox devri.
  - plan/TASK_STANDARD.md — ileriye dönük görev yürütme kuralı bölümü.
  - plan/v2.0/integration-certification/V20-INT-003-yemeksepeti-certification.md — In scope'a devir satırı.
  - plan/v2.0/acceptance/V20-UAT-001-service-flow-acceptance.md — In scope'a ekran okuyucu satırı.

## In scope

1. G1 geçmiş kaydı: 13 sapmanın commit ve yol bazında listesi. Görev sürerken eklenen yollar Semih
   2026-09-26 kararıyla onaylı sahiplik olarak sabitlenir (bu kayıt `evidence/V12-GOV-005/` altındadır).
2. G1 ileriye dönük kural (`plan/TASK_STANDARD.md`):
   - Uygulamadan önce görev dosyası, eksiksiz Owned surface ile `InProgress` durumunda ayrı commit olarak atılır.
   - Görev sürerken görev dosyasında yalnız Status ve Assignee değişir; sonuçlar ve kanıt
     `evidence/<Task-ID>/` altına yazılır.
   - Paylaşılan dosyalar yalnız "Sınırlı ek" alt maddesi olarak ve ters tırnaksız bildirilir.
   - Her commit öncesi `task_scope_tool.py --task-id <ID> --diff-base <InProgress commit>` exit 0 verir.
3. G1 araç desteği: `task_scope_tool.py` Owned surface içindeki "Sınırlı ek" maddesinin ve alt maddelerinin
   ters tırnaksız yol biçimli parçalarını yazma izin listesine ekler; sahiplik (plan_audit çakışma kontrolü)
   değişmez. Testler `test_task_scope.py`'ye eklenir.
4. G2: GATES feragat metnine ve `C104`'e, `V12-ONL-004` ve `V12-ONL-005`'in karşılanmamış sandbox teslim
   kalemlerinin adıyla `V20-INT-003`'e devredildiği yazılır. `V20-INT-003` In scope'una devir satırı eklenir.
5. G3: `V20-UAT-001` In scope'una V12-OUI-001 ekranının NVDA/VoiceOver elle testi eklenir. `df4efdf0`'daki
   görev dışı dosya değişikliği kanıtta kayda geçirilir.
6. G4: `plan_audit_tool.py` iki feragatin `task_scope_tool.py` sabitlerini de okuyup kendi sabitleriyle
   karşılaştırır; uyuşmazlık fail-closed hata üretir.

## Out of scope

- Faz 3 kod bulguları; her biri kendi V12-RMD/V1-RMD görevinde düzeltilir.
- `V0-YSP-001`'i kapatmak veya bir görevi "sandbox doğrulandı" saymak.
- Geçmiş commit'leri yeniden yazmak.

## Dependencies

- V12-GOV-004

## Onay

Approved by Semih — Founder/Product Owner — 2026-09-26: "En küçük hata bile kritik kabul et."; sandbox kalemleri
için "Feragate açıkça ekle"; task_scope için "Yönetişim görevi + ileriye kural".

## Deliverables

- `tools/task-scope/task_scope_tool.py`: "Sınırlı ek" paylaşılan yazma izni.
- `tools/plan-audit/plan_audit_tool.py`: task_scope feragat sabitlerinin çapraz doğrulaması.
- `plan/TASK_STANDARD.md` kural bölümü, `C104`, GATES ve V20 devir satırları.
- `evidence/V12-GOV-005/` geçmiş sapma kaydı ve kapanış kanıtı.

## Acceptance evidence

- `python -m pytest tests/Architecture/TaskScope/ -q` yeşil; yeni testler var.
- Mutasyon: paylaşılan izin kaldırılınca ve çapraz doğrulama kaldırılınca testler kırmızıya döner.
- `plan_audit_tool.py validate` 0/0, `consistency_audit.py` temiz.
- Bu görevin kendi uygulama commit'i `task_scope_tool.py --task-id V12-GOV-005 --diff-base <InProgress commit>`
  ile exit 0 verir.

## Handoff

- V20-INT-003
- V20-UAT-001
