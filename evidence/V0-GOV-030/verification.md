# V0-GOV-030 verification

Task: `V0-GOV-030` — regenerate gate closure evidence counts (CORR:C42).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master`.

## Değişiklikler

- `evidence/v0/gate-v0-exit-closure.md`: mekanik sayım gerçek plan metadata
  tam okumasıyla yeniden üretildi (68 task, 57 `Done`, 11 `Blocked`,
  0 InProgress/Planned). Tarihsel hata kaydı: 2026-08-03 kaydının
  `62/15/47` sayımı (69ae032 revert sonrası eksik), 2026-08-02 `54/33/21`
  sayımı ve C42 dosyasındaki 66/55/11 anlık görüntüsü geçersiz kılındı
  (GOV-031/C43 ve GOV-030 eklenmesi sonrası toplam 68). Open/Closed kararının
  tek kaynağı `plan/GATES.md` C41/C42/C43 kayıtları olarak referanslandı;
  bu evidence kararı üretmez, yalnız doğrular.
- `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json`: araçla yeniden üretildi
  (bu görevin Owned surface'ında).

## Sayımın üretilişi

V0 task dosyaları (`plan/v0/**`, `V0-` önekli 68 dosya) üzerinde
`^- Status:` metadata okuması:

```console
total=68 done=56 blocked=11 planned=1 inprog=0   (GOV-030 InProgress öncesi)
→ GOV-030 Done sonrası: done=57, blocked=11, planned=0, inprog=0
```

## Komutlar ve exit code'lar

### Kapsam ve plan doğrulaması

```console
> py tools/task-scope/task_scope_tool.py --task-id V0-GOV-030 --format text
OK: All changes within scope for V0-GOV-030
exit=0

> py tools/plan-audit/plan_audit_tool.py validate            -> errors 0 (288 md, 267 task, 939 edge)
> py tools/plan-audit/plan_audit_tool.py validate-coverage   -> errors 0
> py tools/plan-audit/plan_audit_tool.py generate-audit-report -> exit 0 (194 added records)
> py tools/plan-audit/plan_audit_tool.py generate-manifest   -> exit 0 (405 md)
> py tools/plan-audit/plan_audit_tool.py verify-manifest     -> errors 0 (405 md, added-file hashes 193)
```

## Kabul koşulları

- [x] `validate`, `validate-coverage`, `verify-manifest` exit code 0.
- [x] Evidence sayımı gerçek plan durumuyla eşit: 68/57/11 (0/0).
- [x] `51/62` ve `62/15/47` sayımları tarihsel hata kaydında; hiçbir sayım
      silinmedi, tümü geçersiz kılındı.
- [x] Kapanış kararı tek kaynağa bağlandı: `plan/GATES.md` C41/C42/C43.

## Kapanış notları

- Task durumu kapsam kontrolünden sonra `Done` yapıldı (araç yalnız
  `Planned`/`InProgress` doğrular).
