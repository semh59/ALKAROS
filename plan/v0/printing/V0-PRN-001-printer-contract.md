# V0-PRN-001 - Validate kitchen printer contract

- Task ID: V0-PRN-001
- Status: Blocked
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Done

## Source basis

- PDF:I.6
- PDF:I.6.6

## Goal

İki mutfak yazıcısının bağlantı, durum algılama, retry ve fiziksel duplicate davranışını gerçek cihazla belirlemek.

## Owned surface

- `evidence/v0/integrations/V0-PRN-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- ESC/POS capability, network/USB mode, paper-out, offline, reconnect, acknowledgment ve duplicate labeling.

## Out of scope

- Production printer adapter kodu yazmak.

## Dependencies

- V0-ARC-003

## Blocker

- Onaylı printer model/firmware/transport listesi ile paper-out, disconnect ve crash-window gerçek cihaz transcript
  kanıtı çalışma alanında yoktur.
- Görev ancak iki yazıcının exact model/firmware/transport kimliği ve bu cihazlara test erişimi sağlandığında `Planned`
  durumuna alınabilir. Paper-out, disconnect ve crash-window transcript'leri `Done` acceptance kanıtıdır.

## Deliverables

- V0-PRN-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Gerçek cihazda paper-out, disconnect ve crash-window test sonuçları kaydedilmiş; exactly-once iddiası yapılmamış.

## Handoff

- V1-KIT-003
- V1-KIT-004
