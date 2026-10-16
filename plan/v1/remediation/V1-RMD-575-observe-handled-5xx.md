# V1-RMD-575 - Uç noktanın kendi yanıtladığı 5xx hatalarını izleme

- Task ID: V1-RMD-575
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Bir uç nokta veritabanı hatasını kendisi yakalayıp 5xx döndüğünde de hata olayı üretilir (V1-RMD-540 bulgusu: masa yönetimi 503'ü olaysız kaldı).

## Owned surface

- `plan/v1/remediation/V1-RMD-575-observe-handled-5xx.md`
- `evidence/V1-RMD-575/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/** - yalnız yanıt durum kodu gözlemi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerDetectors/** - yalnız yeni testler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- 5xx yanıtı olup istisna işleyicisine uğramayan istekler için olay üretimi; aynı istek için çift olay üretilmez.

## Out of scope

- Bu görevin hedefi dışındaki izleme değişiklikleri.

## Dependencies

- V1-RMD-540

## Acceptance evidence

1. Gerçek Postgres testi: uç nokta içinde yakalanan veritabanı hatası olay üretir.
2. İstisna işleyicisinden geçen istek tek olay üretir.
3. Mutasyon kanıtı `evidence/V1-RMD-575/` altındadır.

## Handoff

- None
