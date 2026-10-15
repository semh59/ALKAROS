# V1-RMD-560 - Sunucu dedektörleri uçtan uca test

- Task ID: V1-RMD-560
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

V1-RMD-534 gözlemcisinin gerçek istek hattında (kayıt sırası dahil) çalıştığı gerçek HTTP isteğiyle kanıtlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-560-server-detectors-end-to-end.md`
- `evidence/V1-RMD-560/**`
- `tests/Host/Observability/ServerEndToEnd/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Gerçek Kestrel sunucusunda 500 atan uç nokta tam 1 Critical olay üretir, istemci yine standart hata zarfını alır; 404 üretmez.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-534

## Acceptance evidence

1. Gerçek HTTP isteğiyle 500 = 1 olay ve zarf; 404 = 0 olay; gözlemci kaydı kaldırılırsa test başarısız olur (mutasyon).
2. Çıktılar `evidence/V1-RMD-560/` altındadır.

## Handoff

- None
