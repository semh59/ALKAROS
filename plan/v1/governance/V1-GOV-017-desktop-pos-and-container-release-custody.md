# V1-GOV-017 - Desktop POS and container release custody

- Task ID: V1-GOV-017
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-08-28

## Goal

Reddedilen kart ızgarası uygulamasının ötesinde production deneyimi sözleşmesini genişletmek; masaüstü öncelikli restoran
salon çalışma alanı, operasyonel hesap bölme, ciddi menü yönetimi ve tam container sürümü için sıralı, tek sahipli bir
teslimat zinciri kurmak.

## Owned surface

- `plan/v1/governance/V1-GOV-017-desktop-pos-and-container-release-custody.md`
- `docs/product/V1_DESKTOP_POS_AND_CONTAINER_RELEASE_DECISION_2026-08-28.md`
- `plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md`
- `plan/v1/remediation/V1-RMD-027-operational-bill-splitting-api.md`
- `plan/v1/remediation/V1-RMD-028-desktop-floor-plan-workspace.md`
- `plan/v1/remediation/V1-RMD-029-seat-aware-order-and-bill-split-workspace.md`
- `plan/v1/remediation/V1-RMD-030-menu-management-desktop-quality.md`
- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-032-integrated-designer-and-release-acceptance.md`
- `plan/v1/README.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V1-GOV-017/**`

## Dependencies

- V1-GOV-009
- V1-RMD-025
- V1-BIL-002
- V1-BIL-004

## Acceptance evidence

- Karar; masaüstü bilgi hiyerarşisini, mekânsal salon anlamını, masa/sandalye/kişi ve hesap bölme sınırlarını, gerekli
  durumları, responsive sadeleşmeyi, etkileşim sıralarını, erişilebilirliği ve otonom tarayıcı kanıtını tanımlar;
  `WebPrototype` doğruluk kaynağı sayılmaz.
- `V1-RMD-026` ile `V1-RMD-032` arasındaki görevler exact, çakışmayan owned surface ve sıralı dependency zincirine
  sahiptir; veritabanı/API, salon çalışma alanı, hesap bölme, katalog kalitesi, container sürümü ve son tasarımcı/sürüm
  kabulü tek uygulama diff'inde birleştirilmez.
- Docker kabulü; Host, PosTerminal asset'leri, PostgreSQL 18, sıralı migration'lar, secret yönetimi, HTTPS/proxy sınırı,
  health check'ler, kalıcı volume'lar, başlangıç/yeniden başlatma davranışı ve production secret ya da sahte başarı
  verisi gömmeden tek komutlu işletim yolunu kapsar.
- Plan doğrulaması, audit manifest doğrulaması, Markdown lint ve `git diff --check` owned plan yüzeyinde exit code `0`
  verir.

## Handoff

- V1-RMD-026
