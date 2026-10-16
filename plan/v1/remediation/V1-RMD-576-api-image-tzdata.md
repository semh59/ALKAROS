# V1-RMD-576 - Docker api imajına saat dilimi verisi ekleme

- Task ID: V1-RMD-576
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Docker api imajında `Europe/Istanbul` bulunamadığı için online sipariş fatura taslağı türü başlatılamıyor (V1-RMD-540 bulgusu); imaja `tzdata` eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-576-api-image-tzdata.md`
- `evidence/V1-RMD-576/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/Dockerfile - yalnız api aşamasındaki paket satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- api aşamasına `tzdata` paketi; imaj derlenir ve kapsayıcıda `Europe/Istanbul` çözülür.

## Out of scope

- Bu görevin hedefi dışındaki izleme değişiklikleri.

## Dependencies

- V1-RMD-540

## Acceptance evidence

1. Kapsayıcıda saat dilimi çözümü çıktısı ve api günlüğünde `TimeZoneNotFoundException` yokluğu `evidence/V1-RMD-576/` altındadır.

## Handoff

- None
