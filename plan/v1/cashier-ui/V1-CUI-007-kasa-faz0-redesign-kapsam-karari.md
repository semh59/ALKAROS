# V1-CUI-007 - Kasa Faz 0 redesign kapsam kararı

- Task ID: V1-CUI-007
- Status: Done
- Assignee: claude-sonnet-5-session-01Xsqh6z1RYhmFapKkHKoBmk
- Work type: decision
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

Kasa modülü redesign'ının hangi yüzeyleri kapsayacağını, hangi token setine
geçeceğini ve V1.3'ün henüz teslim edilmemiş nakit tahsilat backend'ini
kapsam dışı bırakan sınırı tek bir kararla sabitlemek.

## Owned surface

- `plan/v1/cashier-ui/V1-CUI-007-kasa-faz0-redesign-kapsam-karari.md`
- `docs/engineering/kasa-faz0-redesign-requirements.md`
- `evidence/V1-CUI-007/**`

## In scope

- `src/Clients/Cashier` (vanilla JS) ve `src/Clients/PosTerminal/src/design-system/tokens.css`
  için Faz 0 token migrasyon kararı ve gerekçesi.

## Out of scope

- Kod değişikliği (bu görev yalnız karar üretir; uygulama V1-CUI-008/009'da).
- V1.3 CashSession/tender UI tasarımı.

## Dependencies

- None

## Deliverables

- `docs/engineering/kasa-faz0-redesign-requirements.md`: kapsam, reddedilen
  alternatif, etkilenen görev kimlikleri.

## Acceptance evidence

- Karar dokümanı source basis, tarih, approver, seçilen sonuç ve reddedilen
  alternatifi içerir.
- V1-CUI-008 ve V1-CUI-009 bu karara dependency verir.

## Handoff

- V1-CUI-008
- V1-CUI-009
