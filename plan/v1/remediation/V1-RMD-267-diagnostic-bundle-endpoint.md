# V1-RMD-267 - Redakte edilmiş tanılama paketi yönetici uç noktasından üretilebilir

- Task ID: V1-RMD-267
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-SUP-001` `DiagnosticBundleService` (destek incelemesi için
redakte edilmiş, boyut ve zaman sınırlı paket) yazılmış ve DI'da kayıtlıydı ama çalışan
uygulamada çağıranı yoktu. `POST /api/v1/management/security/diagnostic-bundle` uç noktası
eklendi: yalnız yönetici oturumu + `security.manage` (V1-RMD-266'nın grubu ve filtresi).

Gövde: korelasyon kimlikleri, zaman penceresi ve gerekçe. Paketi isteyen yönetici
`RequestedByActorId` olarak kaydedilir; servis kendi üretim izini denetim günlüğüne yazar.
Redaksiyon servisin kendisindedir (iki geçiş); bu görev yalnız yüzeyi açar ve hata
gövdelerini Türkçeye çevirir (`NO_CORRELATION_IDS`, `TIME_WINDOW_TOO_LARGE` 400;
`BUNDLE_TOO_LARGE` 413; gerekçe eksikse 400).

## Owned surface

- `plan/v1/remediation/V1-RMD-267-diagnostic-bundle-endpoint.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (V1-RMD-266 sahipliğinde kalır — yalnız `diagnostic-bundle` uç noktası, iki DTO ve gözlemlenebilirlik kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (aynı sahiplikte — 3 yeni test)

## In scope

1. Uç nokta, hata eşlemesi ve yönetici-yalnız yetkilendirme.
2. Gerçek modül bileşimi + gerçek Postgres testleri.

## Out of scope

- Paketi dosya olarak indirme/şifreleme ve yönetim arayüzü.
- Redaksiyon kurallarının değiştirilmesi.

## Dependencies

- V15-SUP-001
- V1-RMD-266

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 6/6. Yeni testler: denetim olayındaki
  `{"password":"hunter2-secret"}` paket çıktısında YOK, görünür alan var, tek kayıt, isteyen yönetici
  kaydedilmiş; korelasyon kimliği yoksa / pencere 30 günü aşarsa / gerekçe boşsa 400; anonim 401,
  yalnız `reports.view` olan yönetici 403.
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` çalıştırıldı.

## Handoff

- None
