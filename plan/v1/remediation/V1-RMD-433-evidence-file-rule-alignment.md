# V1-RMD-433 - Kanıt dosyası kuralının tek standarda bağlanması

- Task ID: V1-RMD-433
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: documentation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

`AGENTS.md` kapanış kapısı kanıtın `evidence/<Task-ID>/` altına kaydedilmesini ister; `plan/TASK_STANDARD.md` ise bu klasörün zorunlu olmadığını ve komut çıktısının ayrı dosyaya kaydedilmediğini söyler. Semih'in 2026-09-28 kararıyla, bir görev `Done` olurken kabul komutlarının gerçek çıktısının `evidence/<Task-ID>/` altında bulunması zorunlu hale getirilir ve iki belge bu tek kurala göre düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-433-evidence-file-rule-alignment.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/TASK_STANDARD.md AGENTS.md
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/VALIDATION_CONTRACT.md — yalnız aynı eski ifadeyi ("ayrı dosyaya
  kaydedilmez") taşıyan kabul kanıtı maddesi

## In scope

1. `plan/TASK_STANDARD.md` içindeki "bu klasör zorunlu değildir" ve "ayrı dosyaya kaydedilmez" ifadeleri, `Done` için gerçek komut çıktısını zorunlu kılan tek kurala çevrilir.
2. `AGENTS.md` kapanış kapısı bölümü aynı kurala atıf yapar.

## Out of scope

- Geçmişte kanıt klasörü olmadan kapanmış görevler; kural yalnız ileriye dönük uygulanır.

## Dependencies

- None

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `python tools/consistency-audit/consistency_audit.py` temiz sonuç verir.
- Semih için senaryo: Claude bir görevi kanıtsız `Done` yapıp commit etmeye çalışınca kapanış kapısının durdurduğu ve belgelerin artık aynı şeyi söylediği görülür.
