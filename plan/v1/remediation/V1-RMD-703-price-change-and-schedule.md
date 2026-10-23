# V1-RMD-703 - Fiyat değişikliği ve ileri tarihli fiyat

- Task ID: V1-RMD-703
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulguları B002, B003 ve B005 kapanır. Yeni satış fiyatı, önceki fiyatın bitişini kendi başlangıcına çekerek aynı işlemde eklenir; böylece fiyat zammı ve "yarından geçerli" fiyat çalışır. Henüz başlamış hiçbir tarihli fiyatı olmayan ürünün başlangıç fiyatı, ileri tarihli ilk fiyat eklenince veya süpürmede silinmez. Satış fiyatı yalnız TRY kabul edilir; böylece başka para birimi satış fiyatını bozamaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-703-price-change-and-schedule.md`
- `evidence/V1-RMD-703/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Catalog/** - yalnız fiyat ekleme, yeniden hesap ve süpürme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Catalog/Pricing/** - yalnız önceki fiyatı kapatarak ekleme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Catalog/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Catalog/Pricing/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Önceki fiyatı kapatarak ekleme, ilk fiyat koruması (uç ve süpürme), TRY dışı satış fiyatının reddi, testler.

## Out of scope

- Fiyat ekranında bitiş tarihi ve geçmiş fiyat düzeltme (V1-RMD-600 B012 ile ayrı iştir).
- Fiyat önbelleği yazımının tek işleme alınması (B015, Düşük) ayrı iştir.

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-703/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
