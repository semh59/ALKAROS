# V1-RMD-457 - Yönetim alanı: Roller ve izinler ekranı

- Task ID: V1-RMD-457
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

V1-RMD-456 rol, izin ve kullanıcı listeleme uçlarını ekledikten sonra, Yönetim alanındaki Personel ve roller bölümüne
(V1-RMD-449) rol yönetimi eklenir: rolleri ve izinlerini görme, rol oluşturma, role izin verme ve geri alma, personele rol
atama ve geri alma. Her işlem kendi `identity.roles.manage` / `identity.permissions.manage` / `identity.users.manage` iznini
sunucuda denetler; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-457-management-roles-permissions-screen.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/ - yalnız roller ve izinler ekranı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız bu ekranın Türkçe etiketleri

## In scope

- Rol listesi (izinleriyle), rol oluşturma, izin verme ve geri alma, personel listesinden rol atama ve geri alma.
- Ekranın bilgi notundaki "roller bu ekrandan yönetilemez" cümlesinin kaldırılması.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md); izin kodları ham gösterilmez, Türkçe ad kullanılır.

## Out of scope

- Sunucu tarafı değişiklik (V1-RMD-456); süreli yetki devri (mevcut "Yetki kararları" ekranı).

## Dependencies

- V1-RMD-449
- V1-RMD-456

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; testler izinli ve izinsiz oturumu ayrı ayrı doğrular.
  Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-457/` altındadır.
- Gerçek Host ve veritabanıyla bir rol oluşturulur, bir izin verilir ve bir personele atanır (`gercek-deneme.log`).
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla Yönetim > Personel ve roller'de yeni bir rol oluşturup izin verin ve
  bir personele atayın; izni olmayan oturumda bu düğmelerin görünmediğini görün.

## Handoff

- None
