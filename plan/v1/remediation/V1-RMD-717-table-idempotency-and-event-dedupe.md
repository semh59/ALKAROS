# V1-RMD-717 - Masa idempotency, olay tekilleştirme ve ölü olay yeniden oynatma

- Task ID: V1-RMD-717
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-601 bulgusu B031 kapanır: tekrar gönderimde ayırma, birleştirme ve transfer için `X-Idempotency-Key` (aynı anahtar ilk sonucu tekrar döner, aynı anahtar başka istekle gelirse reddedilir, aynı anda gelen iki istek sıraya girer). Mevcut `idempotency_keys` tablosu kullanılır, yeni migration yoktur. Olay tekilleştirme ve ölü olay yeniden oynatma zaten var: tüketiciler olay kimlik listesiyle tekrar teslimde değişiklik yapmaz (testli) ve ölü mesajlar V1-RMD-288 ekranından listelenip yeniden kuyruğa alınır; bu kalemler için yeni kod yazılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-717-table-idempotency-and-event-dedupe.md`
- `evidence/V1-RMD-717/**`
- `src/Host/Experience/Tables/TableIdempotencyFilter.cs`
- `tests/Host/Experience/Tables/TableIdempotencyHttpTests.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Tables/** - yalnız yeni ve düzeltilen test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız Tables uçlarının çıkarılması
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ayırma, birleştirme ve transfer uçlarında idempotency anahtarı; PosTerminal istemcisinin anahtar göndermesi.

## Out of scope

- İşaretçi onarımı, kat planı sürüm kuralı, düşük bulgular (V1-RMD-716).
- Birleşim ayırmada eksik katılımcı sürümü (V1-RMD-716 kapattı).

## Dependencies

- V1-RMD-716

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-717/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-602
