# V1-RMD-488 - Ürün kâr raporu yönetim ekranı bölümü

- Task ID: V1-RMD-488
- Status: Done
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`V1-RMD-487` raporunu yönetim ekranında gösteren bölüm: tarih aralığı, ürün tablosu (satılan adet, net satış, maliyet, brüt kâr, kâr yüzdesi), "maliyet bilinmiyor" uyarısı ve kontrol bloğu.
Tüm metinler Türkçe, `management-channel-report` bölümü örnek alınır.

## Owned surface

- `plan/v1/remediation/V1-RMD-488-product-margin-report-screen.md`
- `evidence/V1-RMD-488/**`
- `src/Clients/PosTerminal/src/features/management-product-margin-report/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts ve src/Clients/PosTerminal/src/strings.ts — yalnız bu bölümün kaydı ve metinleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kesin yollar görev başlatılırken yazılır. Bölüm `management-product-margin-report` adıyla ve `reports.view` yetkisiyle `management/sections.ts` içine eklenir; rapor `product-margin.v1` sürümünü gösterir.

## Out of scope

- Raporun hesaplanması (`V1-RMD-487`).

## Dependencies

- V1-RMD-487

## Acceptance evidence

- Testler ve ekran denemesi; çıktılar `evidence/V1-RMD-488/` altındadır.

## Handoff

- None
