# V1-RMD-565 - Yönetim ekranı: hata listesi arayüzü

- Task ID: V1-RMD-565
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Müdür, mevcut Sistem bölümünde canlı hata listesini görür; süzer, ayrıntıya bakar, hatayı onaylar veya çözüldü işaretler.

## Owned surface

- `plan/v1/remediation/V1-RMD-565-error-screen-ui.md`
- `evidence/V1-RMD-565/**`
- `src/Clients/PosTerminal/src/features/management-error-events/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- V1-RMD-539 uç noktalarını kullanan liste, süzgeç, ayrıntı, onayla/çözüldü, bildirim kanalı durumu; Türkçe sabit hata türü sözlüğü; ham metin basılmaz.

## Out of scope

- Sunucu uç noktaları (V1-RMD-539).

## Dependencies

- V1-RMD-539

## Acceptance evidence

1. Ekran metinlerinde İngilizce sızıntı taraması 0.
2. Yükleniyor, boş, hata, yetkisiz durumları test edilir.
3. Çıktılar `evidence/V1-RMD-565/` altındadır.

## Handoff

- None
