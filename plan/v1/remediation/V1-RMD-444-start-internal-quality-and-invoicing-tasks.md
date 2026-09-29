# V1-RMD-444 - Dış bağımlılığı olmayan yedi planlı görevin başlatılması

- Task ID: V1-RMD-444
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: governance
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in kararı (2026-09-29): cari hesap zincirinden sonra, bağımlılıkları tamamlanmış ve dış karar ya da cihaz
beklemeyen planlı görevlere geçilir: katman kalitesi görevleri, yönetim alanı kararı, garsonun çevrimdışı
iptal/ikram bütçesi ve dönemsel fatura kaynak seçimi.

Kapsam denetimi, master'da Planned olan bir görevin dosyasında bir PR'da yalnız Status/Assignee satırlarının
değişmesine ve Owned surface'in master'daki hâlinden okunmasına izin verir. Bu yüzden görevler önce bu yönetim
göreviyle başlatılır: V1-RMD-429, V1-RMD-431, V1-RMD-432, V1-RMD-433, V1-RMD-296, V1-RMD-297 ve V14-INV-001
`InProgress` olur ve uygulamanın dokunması gereken paylaşılan dosyalar "Sınırlı ek" olarak eklenir. Uygulama ve Done
geçişi her görev için master'a birleştirmeden sonra ayrı PR'da gelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-444-start-internal-quality-and-invoicing-tasks.md`
- `evidence/V1-RMD-444/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-429-api-route-authorization-architecture-test.md,
  plan/v1/remediation/V1-RMD-431-shared-api-error-handler.md,
  plan/v1/remediation/V1-RMD-432-posterminal-lint-accessibility.md,
  plan/v1/remediation/V1-RMD-433-evidence-file-rule-alignment.md,
  plan/v1/remediation/V1-RMD-296-decision-management-area-ui.md,
  plan/v1/remediation/V1-RMD-297-offline-authorized-void-comp-reconciliation.md ve
  plan/v1.4/invoicing/V14-INV-001-periodic-source-selection.md — yalnız Status, Assignee ve Sınırlı ek satırları

## In scope

- Yedi görev dosyasının başlatılması; kod değişikliği yok.
- V1-RMD-430 başlatılmaz: bağımlı olduğu V1-RMD-429 Done olunca başlar.

## Out of scope

- KVKK saklama yürütmesi ve kritik yol yük testleri: V14 çıkış kapısı (QNB, fatura, kartla cari tahsilat görevleri)
  kapanmadan başlatılamaz.
- Hugin, QNB, Migros Yemek, yemek kartı, mali belge ve sürüm/sertifikasyon görevleri (dış karar ya da cihaz
  bekliyor).

## Dependencies

- V1-RMD-441

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` ve `python tools/consistency-audit/consistency_audit.py`
  0 hata / 0 uyarı; yedi görevin kapsam denetimi meta veri hatası vermez (`evidence/V1-RMD-444/audit.log`).
- Semih'in elle deneyebileceği senaryo: yok; yalnız plan kaydı.

## Handoff

- V1-RMD-429
