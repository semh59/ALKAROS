# V0-PRN-001 - Validate kitchen printer contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

İki mutfak yazıcısının bağlantı, durum algılama, retry ve fiziksel duplicate davranışını gerçek cihazla belirlemek.

## Owned surface

- `evidence/v0/integrations/V0-PRN-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- ESC/POS capability, network/USB mode, paper-out, offline, reconnect, acknowledgment ve duplicate labeling.

## Out of scope

- Print queue production kodu.

## Dependencies

- V0-ARC-003

## Deliverables

- V0-PRN-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek cihazda paper-out, disconnect ve crash-window test sonuçları kaydedilmiş; exactly-once iddiası yapılmamış.

## Handoff

- V1-KIT-003 ve V1-KIT-004.

