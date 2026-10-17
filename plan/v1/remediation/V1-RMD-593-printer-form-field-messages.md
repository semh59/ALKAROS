# V1-RMD-593 - Yazıcı formunda alan bazlı Türkçe hata mesajları

- Task ID: V1-RMD-593
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Yazıcı ekleme formunda boş ad, boş istasyon, hatalı IP biçimi veya geçersiz port girilince sunucu tek bir genel cümle döndürüyor. Form, sunucuya gitmeden önce hangi alanın neden hatalı olduğunu Türkçe söyler; sunucu doğrulaması aynen kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-593-printer-form-field-messages.md`
- `evidence/V1-RMD-593/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-592-docker-end-to-end-proof.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/** - yalnız yazıcı formu doğrulaması
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İstemci tarafı alan doğrulaması (ad, istasyon, IP biçimi, port aralığı) ve her biri için ayrı Türkçe mesaj; testler ve mutasyon kanıtı.

## Out of scope

- Sunucu doğrulama kurallarının değiştirilmesi.

## Dependencies

- V1-RMD-589

## Acceptance evidence

1. Her alan kuralı için test; mutasyonlarla doğrulanmış.
2. Çıktılar `evidence/V1-RMD-593/` altındadır.

## Handoff

- None
