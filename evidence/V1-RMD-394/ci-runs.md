# V1-RMD-394 — CI kanıtı

## Master'ın durumu (değişiklikten önce)

- Son yeşil `production-validation`: run #264 (2026-09-26T08:06Z).
- #277'den itibaren job'lar hiç başlamıyordu (3–5 sn, log yok). Repo public yapıldıktan sonra run #378 yeniden
  koşuldu (run 36385784122, attempt 2):
  - `Task scope enforcement`: `Setup Python` — "The version '3.12.12' with architecture 'x64' was not found for
    Windows 2025". actions/python-versions manifestinde 3.12.11+ için hiç Windows derlemesi yok.
  - `Locked build, tests and supply chain`: `verify-manifest` — `TOTAL_BYTES actual=6339070 manifest=6339069`.
  - `Cashier and WaiterPwa browser E2E`: başarılı.
- Yerel: temiz klonda `dotnet restore ALKAROS.slnx --locked-mode` → çıkış 1, 39 projede `NU1004`
  (`evidence/V1-RMD-393/baseline/restore-locked.log`).

## Bu daldaki koşular (workflow_dispatch, task-id `V1-RMD-393,V1-RMD-394`, diff-base `cc25c0f`)

| Run | Commit | Sonuç |
| --- | --- | --- |
| 36394543911 (#379) | `9c76b21` | Enforce: Python kuruldu, kapsam "OK" yazdı ama job 1 ile çıktı (pwsh sarmalayıcısı son aracın `$LASTEXITCODE`'unu döndürüyor). Validate: plan, manifest, markdownlint, frontend, Node, Python mimari testleri, kilitli restore + build geçti. |
| 36394844650 (#380) | `597cb00` | Enforce: **başarılı**. Validate: kilitli restore + Release build ve build provenance **başarılı**; tam .NET test adımı ilk kırmızı projede (Composition, V12-TGO-003 kaynaklı) durdu — `bash -e` yüzünden kalan projeler hiç koşmadı. E2E: başarılı. |

## Kabul kanıtı eşlemesi

- Kilitli restore: #380 "Locked .NET restore and Release build" başarılı; yerelde temiz klonda çıkış 0.
- `plan_audit_tool.py validate` / `verify-manifest`: #379 ve #380'de başarılı; yerelde 0 hata.
- Enforce job'u Python'u kuruyor ve kapsam doğrulamasını çalıştırıyor: #380 başarılı.
- Kalan kırmızı: tam .NET test adımı — master'da zaten kırmızı olan paketler (Production: V1-RMD-344,
  Composition: V12-TGO-003, CustomerData.AnonymizationState: V14-CST-002); ayrı görevlere aittir.
- Ek düzeltme (aynı dosya, aynı hedef): tam test adımı `set +e` ile her projeyi koşup tüm başarısızları sonda
  raporlar.
