# V1-RMD-535 - Alan dedektörleri

- Task ID: V1-RMD-535
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Yazıcı kopması, Bilinmeyen ödeme sonucu, mutfağa iletilmeyen sipariş, Yemeksepeti/QNB/Trendyol Go entegrasyon hataları, yedek ve disk durumu hata olayı olarak yakalanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-535-domain-detectors.md`
- `evidence/V1-RMD-535/**`
- `src/Host/Observability/DomainDetectors/**`
- `tests/Host/Observability/DomainDetectors/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Her kaynağın mevcut olay ve durum kayıtlarından okuyan dedektör; kaynak modül değişmez; durum düzelince otomatik çözülür.
- Eşikler ayarlanabilir yapılandırmadan okunur.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533
- V1-RMD-555

## Acceptance evidence

1. Her alan için en az 1 enjekte edilen hata olay üretir ve düzelince 1 dakika içinde çözülür.
2. Bilinmeyen ödeme 5 dakikayı aşınca KRİTİK olur.
3. Çıktılar `evidence/V1-RMD-535/` altındadır.

## Handoff

- None
