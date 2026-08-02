# V1-IAM-001 - Implement user authentication

- Task ID: V1-IAM-001
- Status: Done
- Assignee: opencode-v1-iam-001
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- PDF:II.2.1
- PDF:III.3

## Goal

Password verification, active-user check, login/logout ve secure session issuance davranışını uygulamak.

## Owned surface

- `src/Modules/Identity/Authentication/**`, `tests/Modules/Identity/Authentication/**`,
  `database/migrations/V1/V1-IAM-001/**`
- Kapsam genişletme onayı (2026-08-02 kullanıcı onayı): bu task'ın users migration pozisyon kaydı
  `database/MigrationComposition/order.json` (004 stores / 005 users / 006 roles, sonraki pozisyonlar +2 kayar) ve
  karşılık gelen `docs/data/migration-dependency-graph.md` notu; `src/Modules/Identity/ALKAROS.Identity.csproj` içine
  merkezi `Npgsql` PackageReference; bu task'ın test projesinin `ALKAROS.slnx` ve `build/project-manifest.json`
  kayıtları.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Kapsam genişletme onayı (2026-08-02 kullanıcı onayı, ikinci): `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
  içindeki `RealManifestLoadsWithVerifiedOrderFromV0Dat001` testi order.json'a eklenen 2 pozisyon (005 users, 006 roles)
  nedeniyle 29 -> 31 pozisyon beklentisine güncellendi (Phase A 24 -> 26, Phase B 5 sabit, pozisyon indeksleri 20 ve 24 -> 26).

## In scope

- Kullanıcı kimlik bilgileri depolama, parola karma politikası, oturum açma/oturumu kapatma ve tekrarlanan başarısız
  denemelerde oturum kilidi (lockout) uygulayan arıza yanıtları.

## Out of scope

- Rol izinleri, cihaz kaydı ve şifre sıfırlama workflow.

## Dependencies

- V1-FND-001
- V0-ARC-002

## Deliverables

- `src/Modules/Identity/Authentication/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Geçerli oturum açma başarılı oldu; geçersiz/etkin olmayan kullanıcı, kimlik bilgisi sızıntısı olmadan başarısız olur;
  saklanan değerler tuzlanmış şifre karmalarıdır.

## Handoff

- V1-IAM-002
- V1-IAM-003
