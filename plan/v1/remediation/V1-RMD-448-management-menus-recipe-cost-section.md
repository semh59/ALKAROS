# V1-RMD-448 - Yönetim alanı: Menüler ve tarif maliyeti bölümü

- Task ID: V1-RMD-448
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-296 kararı (Semih, 2026-09-29, öncelik "para ve kapanış önce"): Yönetim alanına Menüler ve tarif maliyeti bölümü eklenir: menüler ve günün menüsü (`/api/v1/management/menus-and-specials`) ile tarif maliyeti anlık görüntüleri (`/api/v1/management/recipes/{recipeVersionId}/cost-snapshots`). Bölüm, oturumun izinlerine göre
görünür; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-448-management-menus-recipe-cost-section.md`
- `src/Clients/PosTerminal/src/features/management-menus-recipe-cost/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ — yalnız bu
  bölümün kaydı; src/Clients/PosTerminal/src/strings.ts — yalnız bu bölümün Türkçe etiketleri

## In scope

- Bölümün ekranları ve istemcisi; yalnız mevcut uçlar kullanılır.
- Yetki matrisi: `menu.manage` menüleri, `inventory.manage` tarif maliyetini yönetir.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Sunucu tarafı değişiklik; yeni uç gerekirse ayrı görevle açılır.
- Diğer bölümler.

## Dependencies

- V1-RMD-445

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; bölümün vitest testleri izinli ve
  izinsiz oturumu ayrı ayrı doğrular. Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-448/` altındadır.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bu bölümün ana işlemini yapın;
  aynı işlemi izni olmayan bir oturumla deneyince düğmenin görünmediğini görün.

## Handoff

- None
