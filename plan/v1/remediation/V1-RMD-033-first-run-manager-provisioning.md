# V1-RMD-033 - First-run manager provisioning

- Task ID: V1-RMD-033
- Status: Done
- Assignee: /root
- Work type: integration
- Surface state: Existing

## Goal

Boş production veritabanını login açısından kullanılamaz bırakan credential boşluğunu kapatmak; ayrı Docker secret ile
tek seferlik, idempotent ve fail-closed ilk manager hesabı, rolü ve V1 permission atamalarını migration sonrasında Host
başlamadan provision etmek.

## Owned surface

- `.dockerignore`
- PO:2026-08-31 kararıyla compose.yaml, deploy/docker/Caddyfile ve deploy/docker/README.md yüzeyleri V1-RMD-079'a devredildi; deploy/docker altındaki diğer yollar bu görevde kalır.
- `deploy/docker/**`
- PO:2026-08-29 kararıyla Program.cs yüzeyi V1-RMD-038'e devredildi; bu historical task closed kalır.
- `tests/Deployment/**`
- `tests/Host/MigrationComposition/Program/FirstRunProvisioningTests.cs`
- `evidence/V1-RMD-033/**`

## Dependencies

- V1-RMD-020
- V1-RMD-030
- V1-GOV-020

## Acceptance evidence

- `provision-manager` komutu kullanıcı adı, görünen ad ve parolayı command line/log yerine environment ile mounted
  secret'tan alır; eksik, boş, satır sonu dışında whitespace içeren veya 12 karakterden kısa parola fail-closed olur.
- Tamamen boş identity şemasında PBKDF2 hash'li aktif manager user, tek manager role, `pos.cashier.mutate` ve
  `catalog.manage` permission kayıtları ve atamaları tek transaction'da oluşur.
- Tekrar çalıştırma parolayı değiştirmez, duplicate üretmez ve beklenen user/role/permission graph'ını doğrular; başka
  kullanıcı varken bootstrap manager yoksa takeover riski nedeniyle fail-closed olur.
- Compose `migrate -> provision -> host -> proxy` sırasını health/exit koşullarıyla uygular. Admin parolası
  `Config.Env`, image, log veya evidence içinde görünmez; CRLF/LF normalize edilir ve Git/build context dışındadır.
- Temiz volume üzerinde gerçek HTTPS browser login; manager katalog, masa, sipariş ve mutfak route görünürlüğü;
  restart/persistence ve yanlış/missing secret failure transcript'leri geçer.

## Handoff

- V1-RMD-031
- V1-RMD-032
