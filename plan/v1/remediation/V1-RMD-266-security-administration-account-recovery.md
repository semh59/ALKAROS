# V1-RMD-266 - Güvenlik yönetimi: hesap kurtarma (tüm oturumları kapat, kilidi kaldır) ve kalıcı denetim izi

- Task ID: V1-RMD-266
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-SEC-002` `AccountRecoveryService` (bir kullanıcının
tüm oturumlarını kapatma, kilidi zamanından önce kaldırma) yazılmış, test edilmiş ve
DI'da kayıtlıydı ama çalışan uygulamada onu çağıran hiçbir yer yoktu; yani bir yönetici
bu işlemleri yapamıyordu. Ayrıca şüpheli-giriş/kurtarma olayları modülün bellek-içi
referans sink'ine yazılıyordu: uygulama yeniden başlayınca denetim izi kayboluyordu.

Bu görev:

1. `security.manage` yetkisini ekler (migrasyon 142): yalnız `manager` rolü; supervisor
   sahip DEĞİLDİR (bir supervisor yöneticiyi dışarıda bırakamamalı).
2. `POST /api/v1/management/security/users/{userId}/revoke-sessions` ve
   `POST .../force-unlock` uç noktalarını açar. Yalnız **yönetici** oturumu
   (`manager:` cihazı; `supervisor:` cihazı yetkisi olsa da reddedilir) ve `security.manage`.
3. `ISuspiciousLoginAuditSink`'i `IAuditEventStore` tabanlı kalıcı bir sink ile değiştirir:
   her kurtarma işlemi ve şüpheli giriş, işlemi yapan kullanıcı ile `audit.audit_events`
   tablosuna yazılır. Giriş uç noktası zaten `SuspiciousLoginAuditingAuthenticationService`
   kullandığından, şüpheli-giriş denetimi de artık kalıcıdır.

Sonraki yönetim işlemleri (yedek, geri yükleme doğrulaması, saklama süpürmesi, sır
rotasyonu, tanılama paketi) aynı grup ve filtreye eklenecek.

## Owned surface

- `plan/v1/remediation/V1-RMD-266-security-administration-account-recovery.md`
- `database/migrations/V1/V1-RMD-266/**`
- `src/Host/Experience/SecurityAdministration/**`
- `tests/Host/Experience/SecurityAdministration/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (yalnız 142 girdisi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (yalnız `PhaseBMax` ve doc-comment)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (yalnız `SecurityManage`; kod listesi ve yalnız-manager katmanı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (yalnız iki satır: `AddSecurityAdministrationExperience`, `MapSecurityAdministrationApi`, ve using)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
  (yalnız yeni kodun sayımı ve rol kapsamı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  (yalnız 142 up/down)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (yalnız 142 için bayat sabitler)
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx
  (yalnız yeni test projesi)

## In scope

1. Migrasyon 142 (`security.manage`, yalnız manager) ve katalog/test güncellemeleri.
2. İki yönetim uç noktası, yönetici-yalnız filtre, Türkçe hata gövdeleri.
3. Kalıcı denetim sink'i.
4. Gerçek modül bileşimi + gerçek Postgres ile HTTP testleri.

## Out of scope

- Yönetim arayüzü ekranı (yalnız HTTP yüzeyi; önceki ölü-modül bağlamalarındaki emsal).
- `SessionRotationService` (V15-SEC-002'nin oturum belirteci döndürme parçası): ne zaman
  döndürüleceği (PIN açılışı, ayrıcalık değişimi, periyodik) bir ürün kararıdır; ayrı görev.
- Diğer güvenlik yönetimi işlemleri (sonraki görevler).

## Dependencies

- V15-SEC-002
- V1-RMD-249
- V1-RMD-250
- V1-RMD-251

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 3/3 (anonim 401, yalnız `reports.view` olan
  yönetici 403, `security.manage` sahibi ama `supervisor:` cihazlı oturum 401 ve hiçbir şey değişmez;
  yönetici tüm oturumları kapatır (2 oturum), denetim olayı işlemi yapan yönetici ile yazılır; kilit
  kaldırılır, sayaç sıfırlanır, bilinmeyen kullanıcı 404).
- Identity.Authorization 206/206 (kod sayısı 22, cashier/waiter/supervisor `security.manage` içermez,
  manager içerir; 142 up/down); Host manifest testleri (161) geçti.
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` ve
  `project_manifest_tool.py` çalıştırıldı.

## Handoff

- None
