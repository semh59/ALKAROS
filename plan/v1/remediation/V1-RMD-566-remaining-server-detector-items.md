# V1-RMD-566 - Kalan sunucu dedektör kalemleri

- Task ID: V1-RMD-566
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

Garson uygulamasının çevrimdışı kuyruk uzunluğu sunucuya bildirilir ve eşik aşımı hata olayı olur; dış çağrılar süreleriyle izlenir ve adresleri olaylara temizlenmiş olarak girer.

## Owned surface

- `plan/v1/remediation/V1-RMD-566-remaining-server-detector-items.md`
- `evidence/V1-RMD-566/**`
- `src/Host/Observability/OfflineQueueReport/**`
- `tests/Host/Observability/OfflineQueueReport/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sunucu uç noktası: kuyruk uzunluğu (sayı) bildirimi, eşik aşımında olay; yalnız sayı ve terminal etiketi alınır.
- Dış çağrı adresleri olaya konmadan önce sunucu adı ve yoldan sorgu/kimlik parçaları atılarak temizlenir.

## Out of scope

- Garson istemcisinin gönderim kodu (V1-RMD-553).

## Dependencies

- V1-RMD-558
- V1-RMD-553

## Acceptance evidence

1. Eşiği aşan kuyruk uzunluğu bildirimi 1 olay üretir; eşik altına inince olay çözülür.
2. Olaylarda sorgu dizesi, kimlik veya anahtar içeren adres bulunmaz.
3. Çıktılar `evidence/V1-RMD-566/` altındadır.

## Handoff

- None
