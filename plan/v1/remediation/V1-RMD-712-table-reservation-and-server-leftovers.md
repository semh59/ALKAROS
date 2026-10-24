# V1-RMD-712 - Rezervasyon ve masa sunucu düzeltmeleri

- Task ID: V1-RMD-712
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulguları B013, B014, B015 ve B016 kapanır. Masa işlemlerinin zaman damgaları sunucu saatiyle yazılır, istemcinin gönderdiği zaman yok sayılır; rezervasyon sipariş kimliği o masaya ait bir sipariş olmalıdır; bitiş zamanı olan rezervasyon süresi dolunca zamanlı işle otomatik sona erer ve elle sona erdirme yalnız süresi dolmuş rezervasyonda çalışır; dolu masada Rezerve komutu sunulmaz; KASA-1 teknik masası masa listesinden çıkar.

## Owned surface

- `plan/v1/remediation/V1-RMD-712-table-reservation-and-server-leftovers.md`
- `evidence/V1-RMD-712/**`
- `src/Host/Experience/Tables/ReservationExpiryHostedService.cs`
- `tests/Host/Experience/Tables/TableServerRulesHttpTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-601-tables-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sunucu saati, rezervasyon sipariş doğrulaması, zamanlı süre dolumu ve elle süre dolumu kuralı, komut listesi, KASA-1 filtresi.

## Out of scope

- Rezervasyona misafir adı, telefonu ve saati eklemek: kişisel veri ve saklama kuralı gerektirdiği için ürün ve mevzuat kararı bekler.
- Kat planı kaydının yerleşim sürümüyle çakışması (B026) ve masa tutarının birleşik ve çok siparişli masada toplanması (B029): V1-RMD-714.
- İstemci ekranları, bitiş saati girişi dahil (V1-RMD-713); veri kısıtları (V1-RMD-714).

## Dependencies

- V1-RMD-601

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-712/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
