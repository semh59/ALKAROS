# V1-RMD-705 - Modifikatör grubunu ürüne bağlama ekranı

- Task ID: V1-RMD-705
- Status: InProgress
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-23

## Goal

V1-RMD-600 bulgusu B004 kapanır. Katalog ekranı ürün-modifikatör grubu atamalarını sunucudan yükler, ürün ayrıntısında bağlı grupları listeler ve yönetici ürüne grup atayabilir. Böylece ekrandan açılan seçenek, kasa ve garsonda satış sırasında seçilebilir hale gelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-705-modifier-group-assignment-screen.md`
- `evidence/V1-RMD-705/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/** - yalnız atama yükleme ve ürün ayrıntısında grup atama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız boş katalog verisine assignments alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-600-catalog-quality-audit.md - yalnız Out of scope notu
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Atama listesini yükleme, ürün ayrıntısında bağlı grupları gösterme, grup atama eylemi, testler.

## Out of scope

- Atamayı kaldırma ucu sunucuda yoktur; ayrı iştir.
- Modifikatör formuna ürün alanı ekleme.

## Dependencies

- V1-RMD-600

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-705/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-601
