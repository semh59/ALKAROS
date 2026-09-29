# V1-RMD-452 - İşlem kimliği testi, fatura oluşturma ve yönetim alanının başlatılması

- Task ID: V1-RMD-452
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: governance
- Surface state: Existing

## Source basis

- PO:2026-09-29

## Goal

Semih'in onayıyla (2026-09-29, "Devam") PR #13'ten sonra bağımlılıkları tamamlanan üç planlı görev başlatılır:
V1-RMD-430 (veri değiştiren uçlarda işlem kimliği testi), V14-INV-002 (fatura oluşturma) ve V1-RMD-445 (Yönetim alanı
kabuğu ile Gün sonu ve mutabakat bölümü). Görevler `InProgress` olur ve dokunacakları paylaşılan dosyalar "Sınırlı ek"
olarak eklenir. V14-INV-003 (fatura satırından kaynağa izlenebilirlik) V14-INV-002'ye bağlı olduğu için `Planned` kalır;
yalnız paylaşılan dosyaları şimdiden tanımlanır. Semih'in aynı günkü kararıyla
faturadaki alıcı vergi kimliği müşteri kaydından gelir; bunun için V1-RMD-453 ayrı görev olarak açılır ve V14-INV-002'den
önce yapılır. Her görevin kodu ve Done geçişi bu kayıt birleştikten sonra ayrı PR'da gelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-452-start-idempotency-invoicing-management-tasks.md`
- `evidence/V1-RMD-452/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-430-mutating-endpoint-idempotency-test.md,
  plan/v1.4/invoicing/V14-INV-002-invoice-generation.md, plan/v1.4/invoicing/V14-INV-003-line-source-traceability.md ve
  plan/v1/remediation/V1-RMD-445-management-area-shell-and-closing.md — yalnız Status, Assignee ve Sınırlı ek satırları

## In scope

- Üç görev dosyasının başlatılması ve V14-INV-003'ün paylaşılan dosyalarının tanımlanması; kod değişikliği yok.

## Out of scope

- Yönetim alanının diğer bölümleri (446–451): 445'in kabuğu birleşince sırayla başlar.

## Dependencies

- V1-RMD-444

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` ve `python tools/consistency-audit/consistency_audit.py`
  0 hata / 0 uyarı; başlatılan görevlerin kapsam denetimi meta veri hatası vermez (`evidence/V1-RMD-452/audit.log`).
- Semih'in elle deneyebileceği senaryo: yok; yalnız plan kaydı.

## Handoff

- V1-RMD-430
