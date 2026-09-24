# V1-RMD-269 - Sürümlü gizli anahtar rotasyonu yönetici uç noktalarından yönetilebilir

- Task ID: V1-RMD-269
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-SEC-001` sürümlü sır rotasyonu (başlat, döndür, iptal, geri al,
geçiş penceresi) yazılmış ve DI'da kayıtlıydı; çalışan uygulamada çağıranı yoktu. Yedek şifreleme
anahtarı (`offsite-backup`) gibi sürümlü anahtarların yönetimi bu olmadan mümkün değildi.

Yönetici oturumu + `security.manage` ile (V1-RMD-266 grubu): `GET /secrets`,
`GET /secrets/{name}/rotation`, `POST .../rotation/initialize|rotate|revoke|rollback`.

Güvenlik kuralları:

- **Anahtar malzemesi API'den geçmez.** Değer dağıtım ortamındadır
  (`ALKAROS_SECRET_{AD}_V{N}`, `EnvironmentVariableSecretProvider`). Rotasyon yalnız hangi sürümün
  etkin olduğunu değiştirir ve malzemesi ortamda olmayan bir sürümü etkinleştirmeyi REDDEDER
  (`KEY_MATERIAL_MISSING`, hangi değişkenin tanımlanacağı Türkçe söylenir): yönetici sistemi
  şifre çözemez bir duruma döndüremez.
- Yalnız bilinen mantıksal adlar yönetilir (`offsite-backup`); bilinmeyen ad 404 (dosya yolu
  enjeksiyonu yok). Yeni ad eklemek bilinçli, gözden geçirilen bir değişikliktir.
- Etkin sürüm iptal edilemez; geçiş süresi 0–720 saat.
- Yanıtlar ve denetim olayları yalnız sürüm numarası/zaman taşır, asla değer.
- Her işlem `secret.rotation.*` denetim olayı olarak (yapan yönetici, gizli ad, sürüm) yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-269-secret-rotation-administration.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecretRotationAdministration.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız `MapSecretRotation()` çağrısı ve iki rotasyon istisnasının Türkçe eşlemesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (3 yeni test ve geçici rotasyon dizini/ortam değişkeni kurulumu)

## In scope

1. Beş rotasyon uç noktası, anahtar malzemesi ön koşulu, bilinen ad listesi, denetim olayları.
2. Gerçek modül bileşimi, gerçek dosya deposu (geçici dizin) ve gerçek Postgres ile HTTP testleri.

## Out of scope

- Anahtar malzemesini ortama yazmak (dağıtım işidir, API'ye alınmaz).
- `IRotatingSecretResolver` (çağıranı yine yok; yedek şifreleyici sürüm kaydını doğrudan okur).
- Süresi dolan geçiş pencerelerini otomatik iptal eden zamanlayıcı.
- Yönetim arayüzü.

## Dependencies

- V15-SEC-001
- V1-RMD-266

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 15/15. Yeni: anonim 401 / yetkisiz 403 /
  bilinmeyen ad 404; malzemesiz `initialize` 409 `KEY_MATERIAL_MISSING` ve hata metni
  `ALKAROS_SECRET_OFFSITE_BACKUP_V1`'i söyler; tam yaşam döngüsü (başlat → tekrar başlat 409 →
  malzemesiz döndürme 409 → döndür → etkin sürümü iptal 409 → aşırı geçiş süresi 400 → geri al →
  liste), yanıtlarda anahtar değerleri YOK, denetim olayları tam sayıda (1/1/1, revoke 0).
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` çalıştırıldı.

## Handoff

- None
