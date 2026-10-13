# V1-RMD-500 - Reçete yönetim ekranı (oluşturma, sürüm, malzeme, etkinleştirme)

- Task ID: V1-RMD-500
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda hazır olan reçete yazma uçlarının (oluşturma, sürüm, malzeme, etkinleştirme, kilitleme, birim dönüşümü ve ürün–reçete eşlemesi) arayüzü yoktur; istemci yalnız okur. Yönetici reçeteyi ekrandan yönetebilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-500-recipe-management-ui.md`
- `evidence/V1-RMD-500/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-menus-recipe-cost/** — yalnız reçete yazma ekranı ve istemci çağrıları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts — yalnız yeni Türkçe metinler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Reçete oluşturma, yeni sürüm, malzeme ekleme/çıkarma, etkinleştirme ve kilitleme.
- Ürün–reçete eşlemesinin ekrandan yapılması.
- Hata kodlarının Türkçe metne çevrilmesi; 409 için yenileme mesajı.

## Out of scope

- Sunucu uçlarının değiştirilmesi.

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-500/` altındadır.

## Handoff

- None
