# V1-RMD-501 - Stok eşleme ve fire geçmişi ekranı

- Task ID: V1-RMD-501
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda hazır olan ürün ve seçenek–stok kalemi eşleme uçları ile fire geçmişi uçları arayüzde çağrılmaz. Yönetici bir ürünün veya seçeneğin hangi stok kaleminden düştüğünü ekrandan görüp değiştirebilmeli, fire kayıtlarını görebilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-501-stock-mapping-and-waste-ui.md`
- `evidence/V1-RMD-501/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/StockSection.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/StockSection.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/stockApi.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/models.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ürün ve seçenek için stok kalemi eşlemelerinin listelenmesi, eklenmesi ve kaldırılması.
- Fire (zayiat) geçmişinin listelenmesi.
- Hata kodlarının Türkçe metne çevrilmesi; ham sunucu metni gösterilmez.

## Out of scope

- Sunucu uçlarının değiştirilmesi.

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-501/` altındadır.

## Handoff

- None
