# V1-RMD-450 - Yönetim alanı: Güvenlik ve sistem bölümü

- Task ID: V1-RMD-450
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-29

## Goal

V1-RMD-296 kararı (Semih, 2026-09-29, öncelik "para ve kapanış önce"): Yönetim alanına Güvenlik ve sistem bölümü eklenir: yedek, RPO, bakım işleri, kilit açma, oturum iptali, tanılama paketi ve sipariş yığını kapatma (`/api/v1/management/security`; bugünkü Güvenlik Yönetimi ekranı bu bölüme taşınır) ile gözlemlenebilirlik uyarıları ve sağlık (`/api/v1/management/observability`). Bölüm, oturumun izinlerine göre
görünür; izni olmayan işlem düğmesi gösterilmez ve sunucunun Türkçe hata gerekçesi ekranda aynen görünür.

## Owned surface

- `plan/v1/remediation/V1-RMD-450-management-security-system-section.md`
- `src/Clients/PosTerminal/src/features/management-security-system/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ — yalnız bu
  bölümün kaydı; src/Clients/PosTerminal/src/strings.ts — yalnız bu bölümün Türkçe etiketleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/SecurityAdministration.tsx ve
  src/Clients/PosTerminal/src/routes/SecurityAdministration.test.tsx — yalnız ekranın bu bölüme taşınması

## In scope

- Bölümün ekranları ve istemcisi; yalnız mevcut uçlar kullanılır.
- Yetki matrisi: `security.manage` güvenlik işlemlerini, `observability.manage` uyarıları yönetir; `reports.view` sağlığı görür.
- Kullanıcıya görünen her metin Türkçe (docs/UI_STYLE_GUIDE.md).

## Out of scope

- Sunucu tarafı değişiklik; yeni uç gerekirse ayrı görevle açılır.
- Diğer bölümler.

## Dependencies

- V1-RMD-445

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; bölümün vitest testleri izinli ve
  izinsiz oturumu ayrı ayrı doğrular. Çıktılar ve bir ekran görüntüsü `evidence/V1-RMD-450/` altındadır.
- Semih'in elle deneyebileceği senaryo: yönetici oturumuyla "Yönetim" sekmesini açıp bu bölümün ana işlemini yapın;
  aynı işlemi izni olmayan bir oturumla deneyince düğmenin görünmediğini görün.

## Handoff

- None
