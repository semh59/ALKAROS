# V1-RMD-445 - Yönetim alanı kabuğu ve Gün sonu ve mutabakat bölümü

- Task ID: V1-RMD-445
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-296 kararı (Semih, 2026-09-29, öncelik "para ve kapanış önce"): PosTerminal'de rol tabanlı bölümleri taşıyan tek "Yönetim" sekmesi kurulur ve ilk bölüm olarak Gün sonu ve mutabakat eklenir: gün sonu kapanışı ve özeti (`/api/v1/management/reporting/business-day`), ödeme mutabakat ve elle onay raporları (`/api/v1/management/payments`) ve mutabakat vakaları (`/api/v1/management/reconciliation/cases`). Bölüm, oturumun izinlerine göre
görünür; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-445-management-area-shell-and-closing.md`
- `src/Clients/PosTerminal/src/features/management-closing/**`
- `src/Clients/PosTerminal/src/features/management/**` (Yönetim alanı kabuğu ve bölüm kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx ve
  src/Clients/PosTerminal/src/strings.ts — yalnız "Yönetim" gezinme öğesi, rota yetkisi ve Türkçe etiketler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.test.tsx ve
  src/Clients/PosTerminal/src/design-system/Icon.tsx — yalnız "Yönetim" sekmesinin testi ve simgesi

## In scope

- Bölümün ekranları ve istemcisi; yalnız mevcut uçlar kullanılır.
- Yetki matrisi: `reports.view` görür; `reports.close-day` gün kapatır; `reconciliation.manage` vakayı çözer.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Sunucu tarafı değişiklik; yeni uç gerekirse ayrı görevle açılır.
- Diğer bölümler.

## Dependencies

- V1-RMD-296

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; bölümün vitest testleri izinli ve
  izinsiz oturumu ayrı ayrı doğrular. Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-445/` altındadır.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bu bölümün ana işlemini yapın;
  aynı işlemi izni olmayan bir oturumla deneyince düğmenin görünmediğini görün.

## Handoff

- None
