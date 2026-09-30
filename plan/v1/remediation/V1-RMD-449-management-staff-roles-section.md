# V1-RMD-449 - Yönetim alanı: Personel ve roller bölümü

- Task ID: V1-RMD-449
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-296 kararı (Semih, 2026-09-29, öncelik "para ve kapanış önce"): Yönetim alanına Personel ve roller bölümü eklenir: roller ve izinler (`/api/v1/management/roles`), personel, pasifleştirme ve süreli yetki devri (`/api/v1/management/users`). Bölüm, oturumun izinlerine göre
görünür; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-449-management-staff-roles-section.md`
- `src/Clients/PosTerminal/src/features/management-staff-roles/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ — yalnız bu
  bölümün kaydı; src/Clients/PosTerminal/src/strings.ts — yalnız bu bölümün Türkçe etiketleri

## In scope

- Bölümün ekranları ve istemcisi; yalnız mevcut uçlar kullanılır.
- Yetki matrisi: Yönetici veya şef garson oturumu bölümü açar; her komut kendi `identity.roles.manage` / `identity.permissions.manage` / `identity.users.manage` iznini sunucuda denetler.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Sunucu tarafı değişiklik; yeni uç gerekirse ayrı görevle açılır.
- Diğer bölümler.

## Dependencies

- V1-RMD-445

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; bölümün vitest testleri izinli ve
  izinsiz oturumu ayrı ayrı doğrular. Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-449/` altındadır.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bu bölümün ana işlemini yapın;
  aynı işlemi izni olmayan bir oturumla deneyince düğmenin görünmediğini görün.

## Handoff

- None
