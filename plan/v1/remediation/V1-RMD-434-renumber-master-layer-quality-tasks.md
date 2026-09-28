# V1-RMD-434 - Master'daki katman kalitesi görevlerinin çakışan kimliklerinin kaydırılması

- Task ID: V1-RMD-434
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

Master'a eklenen katman kalitesi görevleri (V1-RMD-393..397) bu dalın denetim ve düzeltme görevleriyle aynı
kimlikleri kullanıyordu; aynı Task ID iki dosyada olunca plan denetimi ve kapsam kontrolü görevleri ayırt edemez.
Semih'in kararıyla master'daki beş görev V1-RMD-429..433 olarak yeniden numaralandı; bu dalın görevleri ve geçmişi
değişmedi.

Bu görev: master birleştirmesindeki yeniden numaralandırmayı sahiplenir. Beş dosya yeni kimlikle yeniden adlandırılır,
dosya içindeki kimlikler ve aralarındaki bağımlılık (V1-RMD-430 → V1-RMD-429) güncellenir. Görevlerin içeriği,
kapsamı ve durumu değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-434-renumber-master-layer-quality-tasks.md`
- `evidence/V1-RMD-434/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-393-api-route-authorization-architecture-test.md,
  plan/v1/remediation/V1-RMD-394-mutating-endpoint-idempotency-test.md,
  plan/v1/remediation/V1-RMD-395-shared-api-error-handler.md,
  plan/v1/remediation/V1-RMD-396-posterminal-lint-accessibility.md ve
  plan/v1/remediation/V1-RMD-397-evidence-file-rule-alignment.md (master'daki eski adlar) — yalnız silinmeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-429-api-route-authorization-architecture-test.md,
  plan/v1/remediation/V1-RMD-430-mutating-endpoint-idempotency-test.md,
  plan/v1/remediation/V1-RMD-431-shared-api-error-handler.md,
  plan/v1/remediation/V1-RMD-432-posterminal-lint-accessibility.md ve
  plan/v1/remediation/V1-RMD-433-evidence-file-rule-alignment.md — yalnız yeni ad, başlıktaki ve gövdedeki kimlikler

## In scope

- Beş dosyanın adı, Task ID satırı, başlığı, kendi kanıt yolu ve bağımlılık satırı.

## Out of scope

- Görevlerin hedefi, kapsamı, durumu ve atanan kişisi (sahipleri değiştirir).

## Dependencies

- None

## Acceptance evidence

- Her Task ID tek dosyada geçer; `python tools/plan-audit/plan_audit_tool.py validate` ve
  `python tools/consistency-audit/consistency_audit.py` 0 hata / 0 uyarı (`evidence/V1-RMD-434/audit.log`).
- Kapsam denetimi `origin/master` tabanına karşı bu PR'ın görevleriyle kapsanmayan yol bırakmaz.
- Semih'in elle deneyebileceği senaryo: yok; plan kaydı değişikliği. Eski V1-RMD-393..397 katman kalitesi işleri
  artık V1-RMD-429..433 adıyla bulunur.

## Handoff

- None
