# V1-RMD-277 - Doğru PIN oturum belirtecini yeniler

- Task ID: V1-RMD-277
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V15-SEC-002` `SessionRotationService` (eski belirteci doğrula, yenisini oluştur, eskisini iptal et)
yazılmış ve DI'da kayıtlıydı; çalışan uygulamada çağıranı yoktu. Semih'in kararıyla belirteç,
ekran kilidi PIN'i doğru girilince döndürülür: PIN, cihazı kimin tuttuğunu yeniden kanıtlar; kilitliyken
ya da başıboşken sızmış bir belirteç bu noktada çalışmaz hale gelir.

`POST /api/v1/auth/unlock` başarılı olunca oturum aynı terminal ve aynı 12 saatlik ömürle yenilenir,
yeni çerez yazılır, eski oturum iptal edilir. Yönetici oturumu zaten her girişte yeni belirteçle
başlıyor (giriş uç noktası eski oturumu iptal edip yenisini açıyor); bu yüzden ek bir işlem gerekmedi.

## Owned surface

- `plan/v1/remediation/V1-RMD-277-session-token-rotation-on-pin-unlock.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (Host sahibi görevlerde kalır — yalnız `auth/unlock` uç noktasında belirteç yenileme)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/07-kiosk-lock.spec.js
  (V1-RMD-260 ile eklenen paketin yeni senaryosu)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-272 sahipliğinde — `SessionRotationService` girişi silindi, diğer girişlerin gerekçeleri V1-RMD-278'e bağlandı)

## In scope

1. Unlock uç noktasında rotasyon, gerçek tarayıcı doğrulaması.

## Out of scope

- Periyodik / ayrıcalık değişiminde rotasyon (karar: yalnız PIN açılışı).
- Cashier ve PosTerminal istemcileri (PIN kilidi yalnız Garson PWA'da var).

## Dependencies

- V15-SEC-002
- V1-RMD-260
- V1-RMD-272

## Acceptance evidence

- WaiterPwa E2E (gerçek Host + Chromium): 41/41. Yeni senaryo: doğru PIN sonrası `alkaros.cashier` çerezi
  değişir, ESKİ belirteçle `session/current` 401 verir, yeni belirteç çalışır, uygulama giriş ekranına
  düşmeden kullanılabilir kalır.
- **Mutasyon kontrolü:** rotasyon kaldırılınca yeni senaryo "çerez değişmedi" ile kırıldı; geri konunca geçti.
- `consistency_audit.py` ve `plan_audit_tool.py validate` temiz.

## Handoff

- None
