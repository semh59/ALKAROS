# V1-RMD-447 - Yönetim alanı: Satın alma ve üretim bölümü

- Task ID: V1-RMD-447
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-296 kararı (Semih, 2026-09-29, öncelik "para ve kapanış önce"): Yönetim alanına Satın alma ve üretim bölümü eklenir: tedarikçi, sipariş ve mal kabul (`/api/v1/management/purchasing`) ile üretim partileri (`/api/v1/management/production`). Bölüm, oturumun izinlerine göre
görünür; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-447-management-purchasing-production-section.md`
- `src/Clients/PosTerminal/src/features/management-purchasing-production/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ — yalnız bu
  bölümün kaydı; src/Clients/PosTerminal/src/strings.ts — yalnız bu bölümün Türkçe etiketleri

## In scope

- Bölümün ekranları ve istemcisi; yalnız mevcut uçlar kullanılır.
- Yetki matrisi: `purchasing.manage` satın almayı, `production.manage` üretimi yönetir.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Sunucu tarafı değişiklik; yeni uç gerekirse ayrı görevle açılır.
- Diğer bölümler.

## Dependencies

- V1-RMD-445

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; bölümün vitest testleri izinli ve
  izinsiz oturumu ayrı ayrı doğrular. Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-447/` altındadır.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bu bölümün ana işlemini yapın;
  aynı işlemi izni olmayan bir oturumla deneyince düğmenin görünmediğini görün.

## Handoff

- None
