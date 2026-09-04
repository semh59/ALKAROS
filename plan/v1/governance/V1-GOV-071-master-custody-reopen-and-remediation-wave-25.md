# V1-GOV-071 - Master custody reopen and remediation wave 25

- Task ID: V1-GOV-071
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-04

## Goal

Semih onayıyla (2026-09-04, `docs/domain/authorization-model.md`) landed edilen
`V1-IAM-016..024` differentiated-authorization dalgası, `V1-GOV-070` ile
kapatılan `GATE-V1-EXIT`'ten sonra master'a indi (yeni migration'lar 044-049,
`waiter` rolü, izin kodu granülerleştirmesi, her Experience endpoint'inin
granüler koda bağlanması) ve kapıyı fiilen yeniden açtı; hiçbir commit bu
yeniden açılışı `plan/GATES.md` üzerinde kaydetmedi. Bu görev gate'i resmen
yeniden açılmış olarak belgeler, dalganın kendi denetiminde
(`docs/engineering/authz-wave-remediation-plan.md`,
`docs/engineering/v1-independent-audit.md`) bulunan kalan kalıntı işi tek bir
kurtarma görevine (`V1-IAM-025`) kaydeder ve kapanış görevini (`V1-GOV-072`)
planlar.

## Owned surface

- `plan/v1/governance/V1-GOV-071-master-custody-reopen-and-remediation-wave-25.md`
- `plan/v1/identity-authorization/V1-IAM-025-authorization-wave-hardening-and-requester-wiring.md`
- `plan/v1/governance/V1-GOV-072-wave25-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-071/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` üzerinde 25. dalga notu ile yeniden
  açılmış olarak güncellemek (`V1-GOV-068`/`V1-GOV-070` deseni).
- `plan/v1/README.md` görev matrisini `V1-IAM-016..024` (9 görev), bu görev ve
  `V1-IAM-025` ile güncellemek; sayaçları ve 25. dalga anlatı satırını
  eklemek.
- `V1-IAM-025`'i `Status: Blocked` olarak,
  `docs/engineering/authz-wave-remediation-plan.md` Phase 2/3/5 kapsamı
  (D1-D3, D7, D8 düşük riskli sağlamlaştırma; A1, D4, D5 şema sağlamlaştırma
  migration'ı; C1-C5, D6 istemci tarafı kablolama) ile kaydetmek — custody
  devri henüz yapılmadığı için Blocker.
- `V1-GOV-072`'yi `Status: Planned` olarak, `V1-IAM-025`'e bağımlı ve
  `V1-IAM-025` `Done` olmadan kapanamayacak şekilde kaydetmek.
- Dalganın `016-024` sürecindeki custody transfer'larını özetlemek:
  `V1-RMD-013`/`026`/`054`/`066`/`097`/`098` ve `V1-IAM-017`/`023`'ten
  `V1-IAM-024`'e (bkz. `V1-IAM-024` dosyasının "Yüzey devri" notları).
- `docs/engineering/authz-wave-remediation-plan.md` Phase 7 (P1, personel
  provisioning bootstrap kararı) ile Phase 1 (E1-E3 model doküman
  düzeltmeleri, bu dalgada `fix(v1-iam-016)` ile ayrıca kapatıldı) durumunu
  not etmek.

## Out of scope

- Kod değişikliği (migration, endpoint, servis, doküman içerik düzeltmesi
  hariç) — bunlar `V1-IAM-025`'in kapsamındadır.
- `GATE-V1-EXIT`'in kesin mühürlenmesi — `V1-GOV-072`, `V1-IAM-025` `Done`
  olmadan kapanamaz.

## Dependencies

- V1-GOV-070

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Kayıtlı kurtarma görevi (`V1-IAM-025`) ve planlanmış kapanış görevi
  (`V1-GOV-072`).

## Acceptance evidence

- `plan/GATES.md` `GATE-V1-EXIT` satırı 25. dalga yeniden açılış notunu
  içerir.
- `plan/v1/README.md` görev sayacı (270 → 273: `V1-GOV-071` `Done`,
  `V1-IAM-025` ve `V1-GOV-072` `Planned`/`Blocked`) ve 25. dalga anlatı
  satırı güncellenmiştir.
- `V1-IAM-025` ve `V1-GOV-072` görev dosyaları oluşturulmuş; Dependencies ve
  Handoff zinciri (`V1-GOV-070` → `V1-GOV-071` → `V1-IAM-025` → `V1-GOV-072`
  → `GATE-V11-ENTRY`) tutarlıdır.
- `python tools/plan-audit/plan_audit_tool.py validate`, `validate-coverage`
  ve `verify-manifest` sıfır hata; `plan/AUDIT_MANIFEST.json` ve
  `plan/AUDIT_REPORT.md` mevcut ağaç durumuna göre yeniden üretilmiştir.

## Handoff

- V1-IAM-025
