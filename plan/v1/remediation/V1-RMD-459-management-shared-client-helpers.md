# V1-RMD-459 - Yönetim ekranları: tekrarlanan istemci yardımcılarını tek yerde toplama

- Task ID: V1-RMD-459
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Yönetim alanının yedi bölümü (`management-closing`, `-stock`, `-menus-recipe-cost`, `-purchasing-production`,
`-security-system`, `-settings-history`, `-staff-roles`) aynı HTTP istek yardımcısını (Türkçe sunucu gerekçesini aynen
taşıyan hata sınıfı, durum kodu yedek metinleri, ağ hatası çevirisi, zaman aşımı) ve beş bölüm aynı panel yükleme
bileşenlerini (`usePanel`, `PanelView`, `Notice`) ve aynı test yardımcısını kopyalayarak taşıyor. Bunlar bir kez yazılıp
`features/management/` altından kullanılır; ekranların davranışı ve görünen metinleri değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-459-management-shared-client-helpers.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ - yalnız ortak `http.ts`, `panel.tsx` ve `testKit.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-menus-recipe-cost/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-security-system/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/ - yalnız kopya yardımcıların ortak olanla değiştirilmesi

## In scope

- Ortak istek yardımcısı (hata sınıfı, yedek metinler, zaman aşımı seçeneği, yalnız Yönetim > Gün sonu için POST'ta
  idempotency anahtarı) ve ortak panel/test yardımcıları.
- Bölümlerdeki kopyaların ve yalnız onları kullanan tek amaçlı hata sınıflarının kaldırılması.

## Out of scope

- Davranış, metin, uç noktası ya da izin değişikliği; Yönetim dışı ekranlar.

## Dependencies

- None

## Acceptance evidence

- PosTerminal `pnpm run lint`, `pnpm run typecheck` ve `pnpm test` exit code 0; mevcut bölüm testleri değişmeden
  (yalnız içe aktarma yolları) yeşil. Çıktılar `evidence/V1-RMD-459/` altındadır.
- Gerçek Host ve veritabanıyla Yönetim'in yedi bölümü açılıp veri geldiği görülür (`gercek-deneme.log`).

## Handoff

- None
