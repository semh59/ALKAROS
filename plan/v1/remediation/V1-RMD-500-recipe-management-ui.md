# V1-RMD-500 - Reçete yönetim ekranı (oluşturma, sürüm, malzeme, etkinleştirme)

- Task ID: V1-RMD-500
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Sunucuda hazır olan reçete yazma uçlarının (oluşturma, sürüm, malzeme, etkinleştirme, kilitleme, birim dönüşümü ve ürün–reçete eşlemesi) arayüzü yoktur; istemci yalnız okur. Yönetici reçeteyi ekrandan yönetebilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-500-recipe-management-ui.md`
- `evidence/V1-RMD-500/**`
- `src/Clients/PosTerminal/src/features/management-recipes/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yeni "Tarifler" yönetim bölümü (`inventory.manage` yetkisi): reçete listesi, oluşturma, sürüm listesi, yeni taslak sürüm, malzeme ekleme/çıkarma, sürümü etkinleştirme ve kilitleme.
- Özel birim dönüşümlerinin listelenmesi ve eklenmesi.
- Ürün–reçete eşlemesinin ekrandan yapılması (atama, görüntüleme, kaldırma).
- Hata kodlarının Türkçe metne çevrilmesi; 409 için yenileme mesajı. Ham sunucu metni ekrana basılmaz (V1-RMD-499).

## Out of scope

- Sunucu uçlarının değiştirilmesi; mevcut "Tarif maliyeti" bölümünün değiştirilmesi.

## Dependencies

- V1-RMD-114

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-500/` altındadır.

## Handoff

- None
