# V15-SEC-001 - Harden secret rotation and recovery

- Task ID: V15-SEC-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

V1-SEC-001 secret boundary üzerinde production rotation, failover ve recovery davranışını uygulamak.

## Owned surface

- `src/Modules/Security/SecretRotation/**`, `tests/Modules/Security/SecretRotation/**`, `deployment/secrets/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (module bootstrap — C86/C88/C91 emsali: yeni bağımsız
  foundation görevi oluşturulmaz, yeni bir modülün ilk feature görevi kendi
  proje dosyasını sahiplenir): `src/Modules/Security/ALKAROS.Security.csproj`
  (modül-kök proje dosyası, `SecretRotation/` bu modülün ilk feature'ı),
  `src/Modules/Security/packages.lock.json`,
  `tests/Modules/Security/SecretRotation/packages.lock.json`, `ALKAROS.slnx`
  ve `build/project-manifest.json`'a bu iki projenin kaydı (V1-FND-001
  sahipliğinde).

## In scope

- Versioned rotation, overlap window, revoke, provider outage, rollback ve redacted operational diagnostics.
- Secret saklama mekanizması: Secret'lar `deployment/secrets/**` altında ve repo dışında tutulur; çalışma zamanında
  environment/volume enjeksiyonu ile yüklenir; plaintext settings ve source içinde secret saklama yasağı V0-ARC-005
  kararına uyar.

## Out of scope

- Base secret resolution, production secret değeri, user password hashing ve payload encryption policy.

## Dependencies

- GATE-V15-ENTRY
- V1-SEC-001
- V0-ARC-005

## Deliverables

- `src/Modules/Security/SecretRotation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test
  assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repository/database/log içinde raw secret yoktur: `SecretVersionRecord`/
  `SecretRotationRecord`/`SecretRotationSnapshot` hiçbir alanda ham secret
  değeri taşımaz (yalnız versiyon numarası, status, zaman damgaları); gerçek
  değerler mevcut `ISecretProvider`/`ISecretResolver` sınırından, versioned
  bir `SecretReference` (`{name}-v{n}`) üzerinden okunur — `FileSecretRotationStore`
  yalnız bu metadata'yı `evidence/V15-SEC-001` dışında, çağıranın verdiği
  dizine (örn. `deployment/secrets/rotation-state/`) JSON olarak yazar.
- Başarı/ret/recovery testleri: `dotnet test
  tests/Modules/Security/SecretRotation/ALKAROS.Security.SecretRotation.Tests.csproj`
  → 28/28 (bkz. `evidence/V15-SEC-001/test-secretrotation.txt`) — versioned
  rotation, overlap window, revoke (aktif dahil), rollback (başarı +
  expired-overlap reddi + non-overlap-status reddi), provider outage
  fallback'i (`RotatingSecretResolver`, `SecretNotFoundException`'da bir
  sonraki Overlap versiyonuna düşer), access-denied'ın ASLA başka bir
  versiyona tekrar denenmemesi, atomik dosya-tabanlı persistence round-trip,
  path-traversal/unsafe dosya adı reddi.
- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata
  (`evidence/V15-SEC-001/build-release.txt`); Debug de doğrulandı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (`evidence/V15-SEC-001/plan-audit.txt`).
- `python tools/consistency-audit/consistency_audit.py` → temiz
  (`evidence/V15-SEC-001/consistency-audit.txt`).
- `python tools/project-manifest/project_manifest_tool.py` → VALID, 0 fark
  (`evidence/V15-SEC-001/project-manifest.txt`).

## Handoff

- V20-SEC-001
