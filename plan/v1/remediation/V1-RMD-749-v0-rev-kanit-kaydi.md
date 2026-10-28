# V1-RMD-749 - V0-REV-001..030 kanıt kaydı (739 bulgusu: kanıtsız Done)

- Task ID: V1-RMD-749
- Status: InProgress
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: documentation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

V0-REV-001..030 görevleri Done'dır ve her birinin md'sinde Semih'in 2026-08-15 tarihli onayı ("Karar geçerlidir, yeni provenance paketi beklenmez.") kayıtlıdır; ancak hiçbirinin `evidence/<Task-ID>/` klasörü yoktur. Bu görev her biri için gerçek kaydı üretir: onay satırının md'den birebir alıntısı, görev md'sinin commit sonucu ve gerçek `plan_audit_tool.py validate` çıktısı exit code ile. Teslim dokümanı (supplement) üretilmediği, PO kararının onu gereksiz kıldığı açıkça yazılır; yeni karar veya onay uydurulmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-749-v0-rev-kanit-kaydi.md`
- `evidence/V1-RMD-749/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): evidence/V0-REV-*/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Her V0-REV-001..030 için `evidence/V0-REV-NNN/README.md` ve `evidence/V0-REV-NNN/closing/plan_audit.log`.

## Out of scope

- V0-REV görev md'lerini veya `docs/decision-revalidation/**` teslim dokümanlarını değiştirmek veya üretmek.
- Onay metnini yorumlamak veya genişletmek.

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. 30 kanıt klasörü vardır; her README'deki onay satırı ilgili md'deki `## Onay` bölümüyle birebir aynıdır (doğrulama betiği çıktısı `evidence/V1-RMD-749/verify.log`, exit 0).
2. Her `closing/plan_audit.log` gerçek komut çıktısı ve `exit=0` içerir.
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `evidence/V1-RMD-749/closing/` altındadır.

## Handoff

- V1-RMD-739
