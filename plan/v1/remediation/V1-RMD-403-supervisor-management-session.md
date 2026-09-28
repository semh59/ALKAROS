# V1-RMD-403 - Şef garsona (supervisor) girişte yönetim oturumu vermek

- Task ID: V1-RMD-403
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi (H-03): yönetim çerezi girişte yalnız `catalog.manage` tutan kullanıcıya (`manager:` cihaz oturumu)
veriliyor; `supervisor:` cihaz oturumunu üreten hiçbir kod yok. Oysa `ManagementSessionLookup` bu oturumu onay kararı
ekranı, rol yönetimi, mutabakat vakaları, gün sonu, gözlemlenebilirlik ve stok raporları için kabul ediyor; model §4
adım 3 bekleyen onayların "her vardiyadaki yönetici / şef garson cihazına" gittiğini, §3 karar 3 şef garsonun yoğun
saatte yöneticisiz işi yürüttüğünü söylüyor. Gerçek bir şef garson girişi bugün yalnız kasiyer çerezi alıyor ve onay
ekranı 401 dönüyor. Bu görev: `catalog.manage` tutmayan ama şef garson işaret iznini (`reports.view`, karar ekranının
kendi izni) tutan kullanıcıya girişte `supervisor:` yönetim oturumu verir; çıkış ve yeniden giriş onu da iptal eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-403-supervisor-management-session.md`
- `evidence/V1-RMD-403/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (V1-IAM-024 sahipliğinde) — yalnız giriş ve çıkış ucundaki yönetim oturumu verme/iptal satırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs
  (V1-RMD-175 sahipliğinde) — şef garson girişi testi ve bir izin tohumlama yardımcısı

## In scope

- Giriş: `catalog.manage` → `manager:` oturumu (değişmez); değilse `reports.view` → `supervisor:` oturumu, aynı
  `alkaros.manager` çerezinde; ikisi de yoksa çerez silinir.
- Giriş ve çıkış, o terminaldeki önceki `manager:` ve `supervisor:` oturumlarını iptal eder.
- Yalnız yönetici uçları (`allowSupervisor: false`) şef garson oturumunu reddetmeye devam eder.

## Out of scope

- PosTerminal'in şef garsona hangi yönetim ekranlarını göstereceği (istemci zaten yeteneklere göre süzüyor).
- Oturum ömrü (V1-RMD-399 H-01) — ayrı görev.

## Dependencies

- V1-RMD-402

## Acceptance evidence

- `ALKAROS.Host.Tests` DualScreen sınıfları (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 76/76
  (`evidence/V1-RMD-403/tests.log`; ilgisiz bir Kestrel bağlantı testi dar koşuda bir kez düştü, tam koşuda geçti —
  aynı dosyada not edildi).
- Yeni test `ASupervisorLoginReachesTheGrantDecisionSurfaceButNotManagerOnlyAreas`: `reports.view` tutan, `catalog.manage`
  tutmayan kullanıcı gerçek girişle yönetim çerezi alır; bekleyen onaylar 200; yalnız yöneticiye açık katalog 401;
  çıkıştan sonra aynı çerezle bekleyen onaylar 401.
- Aynı test üretim değişikliği geri alınınca kırmızı (`evidence/V1-RMD-403/red-without-fix.log`: beklenen 200, gelen 401).
- Semih'in elle deneyebileceği senaryo: şef garson hesabıyla PosTerminal'e girip "Onaylar" ekranını açmak; bekleyen
  iptal/ikram istekleri listelenir ve onaylanabilir. Katalog yönetimi yine yalnız yöneticiye açıktır.

## Handoff

- None
