# V1-RMD-694 - Kasa, fatura ve sipariş akışı düzeltmeleri

- Task ID: V1-RMD-694
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B023, B051, B052, B053, B056, B057, B086, B102, B130 kapanır. Tahsilat sonrası hesap kapatma hatası günlüğe düşer ve ayırt edilir; kasa hareketi ve kapanış eşzamanlı çalışmada tutarlı kalır; aktif kasa oturumu ucu sınırsız geçmişi çekmez; aynı ürünün notlu ve modifiyerli satırları yanlış birleşmez; olmayan oturumda beklenen nakit 404 verir; fiş ödeme yöntemlerini doğru adlandırır; kasa hareketi tekrarında tutar ve yön denetlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-694-cash-and-order-flow-fixes.md`
- `evidence/V1-RMD-694/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/** - yalnız kasa, ödeme ve sipariş uçları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/** - yalnız oturum ve hareket depoları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız satır birleştirme ve depo
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-693-error-catalog-and-envelope.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yutulan hataların günlüğü, işlem sınırları, sorgu sınırları, birleştirme imzası, durum kodları, fiş sorgusu, tekrar denetimi, testler.

## Out of scope

- Kart tahsilatı yer tutucu (V13-HUG-001, dış bağımlılık); 'kart çekilmedi' kuralı (Semih sorusu S8).

## Dependencies

- V1-RMD-693

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-694/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-695
