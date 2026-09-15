# V1-IAM-031 - Oturum süresi 12 saatten 8 saate düşürüldü

- Task ID: V1-IAM-031
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in kararı (2026-09-15, "en uygun garson" bildirim tasarımı konuşulurken):
oturum süresi bir çalışma günü uzunluğuna daha yakın olmalı, "ömür boyu
açık kalamaz". `SessionTokenIssuer.DefaultLifetime` 12 saat idi; 8 saate
düşürüldü.

## Owned surface

- src/Modules/Identity/Authentication/SessionTokenIssuer.cs (V1-RMD-001
  sahipliğinde, sınırlı ek) — `DefaultLifetime` 12 saatten 8 saate,
  doc-comment güncellendi.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Modules/Identity/Authentication/*.csproj` → tüm testler
  yeşil (`SessionTokenIssuerTests.IssueAppliesTheDefaultLifetime`,
  `DefaultLifetime`'a sembolik referans verdiği için değer değişikliğinden
  etkilenmedi, hâlâ doğru davranışı kanıtlıyor).
- `dotnet test tests/Modules/Identity/DeviceSessions/*.csproj` → tüm testler
  yeşil (regresyon yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyada 0 ihlal.
- Semih'in elle deneyebileceği senaryo: bir garson oturum açar, 8 saat
  sonra token'ın `identity.device_sessions.expires_at`'i geçmiş olur ve
  bir sonraki istek 401 döner (önceden 12 saat sürüyordu).

## Handoff

- None
