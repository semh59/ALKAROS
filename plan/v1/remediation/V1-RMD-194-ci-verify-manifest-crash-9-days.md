# V1-RMD-194 - CI'ın `verify-manifest` kapısı 9 gündür (~80 push) çöküyordu

- Task ID: V1-RMD-194
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`gh run list` ile bulundu: GitHub Actions'daki `production-validation`
workflow'u **2026-09-04'ten sonraki HER push'ta** (2026-09-07'den bugüne
~80 çalıştırma) `Validate plan, PDF trace and project manifest` adımında
başarısız oluyordu — bu adım başarısız olduğu için pipeline'ın kalanı
(frontend build, .NET build/test, bağımlılık taraması, SBOM, secret
taraması) **hiç çalışmıyordu**. Yerel `plan_audit_tool.py validate` bu
oturum boyunca defalarca çalıştırıldı ve hep temiz döndü — çünkü
`verify-manifest` AYRI bir alt-komut, hiç ayrıca çalıştırılmamıştı.

Kök neden: `2026-09-07`'deki `e2676eec` commit'i ("renumber QR/online
ordering as v1.2, shift Payment and Customer Account back"), `plan/v1.2`,
`plan/v1.3`, `plan/v1.4` arasında 83 görev dosyasını yeniden adlandıran
üç yönlü bir rotasyon yaptı — ama `tools/plan-audit/plan_audit_tool.py`
içindeki `rename_map` sözlüğüne (denetim geçmişinin TEK yeniden adlandırma
kaydı) bu 83 girdiden hiçbiri hiç eklenmedi. Sonuç, iki aşamalı bir
bozulma: önce `plan/AUDIT_REPORT.md`'nin "eklenen dosyalar" tablosu drift
etti (444+ hata, sessizce büyüdü), sonunda `V12-CSH-003-cash-tender-
handler.md` gibi bir dosya artık hiç var olmadığı için `verify_manifest()`
`KeyError`'la SERT ÇÖKTÜ — kalan hataları bile göstermeden.

Kanıt: `gh run list --limit 100` → son başarılı çalıştırma
`2026-09-04T13:23:08Z`; ondan sonraki HER çalıştırma `failure`.
`python3 tools/plan-audit/plan_audit_tool.py verify-manifest` yerel
olarak da aynı `KeyError: 'plan/v1.2/cash/V12-CSH-003-cash-tender-
handler.md'` ile çöktü, birebir CI'daki traceback'in aynısı.

## Owned surface

- `plan/v1/remediation/V1-RMD-194-ci-verify-manifest-crash-9-days.md` (yeni)
- Sınırlı ek:
  - tools/plan-audit/plan_audit_tool.py (plan-audit aracı sahipliğinde) —
    üç yerdeki `rename_map` sözlüğüne (3 kopya, aynı literal) `e2676eec`
    rotasyonunun `git show e2676eec --summary -M10` ile doğrulanmış tüm
    83 gerçek yeniden adlandırması eklendi.
  - plan/AUDIT_REPORT.md (plan-audit aracının kendi çıktısı) — araç
    kendisi `generate-audit-report` ile yeniden üretti: 211 baseline
    satırının "final" sütunundaki 61 bayat yolu (rotasyondan etkilenen
    baseline dosyaları) gerçek güncel yollarına düzeltti, "eklenen
    dosyalar" tablosunu (894 satır) `actual_paths`'tan sıfırdan yeniden
    hesapladı.
  - plan/AUDIT_MANIFEST.json (plan-audit aracının kendi çıktısı) — araç
    kendisi `generate-manifest` ile yeniden üretti.

## Out of scope

- Bu tür bir bozulmanın bir daha sessizce oluşmaması için CI'a
  `verify-manifest`'i ayrı, daha sık çalışan bir adım/uyarı olarak
  eklemek: mevcut CI zaten dört komutu (`validate`, `validate-coverage`,
  `verify-manifest`, `project_manifest_tool.py`) sırayla çalıştırıyor —
  eksik olan CI'ın kendisi değil, hiçbir sonraki push'un CI durumunun
  kontrol edilmemesiydi (bu görevin bulduğu ana süreç boşluğu, ayrı bir
  görev/karar konusu).

## Dependencies

- None

## Acceptance evidence

- Kök neden, `gh run list`/`gh run view --log-failed` ile CI'ın kendi
  gerçek loglarından doğrulandı (yukarıdaki Goal bölümü).
- 83 yeniden adlandırma, `git show e2676eec --summary -M10` çıktısından
  programatik olarak çıkarıldı ve her yeni yolun gerçekten diskte var
  olduğu doğrulandı (`0` eksik).
- Düzeltmeden önce yerel: `python3 tools/plan-audit/plan_audit_tool.py
  verify-manifest` → `KeyError` ile çöktü (CI'daki traceback'in aynısı).
- Düzeltmeden sonra yerel, CI'ın çalıştırdığı tam sırayla dördü de:
  - `python3 tools/plan-audit/plan_audit_tool.py validate` → 0 hata,
    0 uyarı.
  - `python3 tools/plan-audit/plan_audit_tool.py validate-coverage` →
    0 hata.
  - `python3 tools/plan-audit/plan_audit_tool.py verify-manifest` →
    **Manifest errors: 0** (önceden KeyError ile çöküyordu).
  - `python3 tools/project-manifest/project_manifest_tool.py` →
    `Status: VALID (0 differences)`.
- `python3 -c "import ast; ast.parse(...)"` ile `plan_audit_tool.py`'nin
  kendi sözdizimi doğrulandı.

## Handoff

- None
