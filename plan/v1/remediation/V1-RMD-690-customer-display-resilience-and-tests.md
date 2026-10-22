# V1-RMD-690 - Müşteri ekranı: toparlanma, oturum, eşleştirme ve iptal deneyimi, sunucu testleri

- Task ID: V1-RMD-690
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B003, B004, B015, B017, B018, B019, B020, B027, B028, B066, B069, B070, B074, B087, B114, B121, B126, B129 kapanır. Müşteri ekranı ağ, sunucu hatası veya 429 sonrası kendiliğinden toparlanır; oturum süresi etkin kullanımda uzar ve bitişi kasada önceden görünür; yeni eşleşme eski ekranı iptal eder; eşleştirme çakışma ve yanlış kod hataları ayırt edilir; iptal onay ister ve gerçek sayıyı söyler; snapshot yanıtları sıralanır. Eşleştirme, iptal, session/current ve orders/active için sunucu testleri yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-690-customer-display-resilience-and-tests.md`
- `evidence/V1-RMD-690/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız müşteri ekranı oturum ve eşleştirme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/** - yalnız müşteri ekranı ve kasa eşleştirme diyaloğu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/dual-screen-pos-topology.md - yalnız oturum süresi ve eşleştirme
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-689-help-request-delivery-feedback.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Hata sonrası yeniden deneme, kayan oturum süresi, çift ekran, hata kodu ayrımı, iptal onayı, istek sıralama, testler (eşleştirme mutlu ve olumsuz yol, revoke, session/current, orders/active, hub bağlantısı).

## Out of scope

- Table-draft siparişlerinin müşteri ekranında görünmesi (Semih sorusu S9).

## Dependencies

- V1-RMD-689

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-690/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-691
