# V0-DOM-005 - Define table reservation policy

- Task ID: V0-DOM-005
- Status: Blocked
- Assignee: codex-v0-dom-005
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5
- CORR:C5

## Goal

`Table.Reserved` anlamını, bu durumu oluşturabilen actor'ları, expiry davranışını ve walk-in/personel/QR etkileşimini
tanımlamak.

## Owned surface

- `docs/domain/table-reservation-policy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Rezervasyon kimliği/aktör/zaman, doluluk önceliği, sona erme, iptal ve eşzamanlı durum kuralları.

## Out of scope

- Rezervasyon/rezervasyon UI veya uygulama.

## Dependencies

- V0-DOM-001

## Blocker

- Candidate evidence, `V0-DOM-001` `Done` olmadan kabul edilemez; ancak tam
  dependency zinciri kapatılıp acceptance yeniden doğrulanınca görev `Planned` olur.

## Deliverables

- V0-DOM-005 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Ayrılmış durumun kalıcı bir sahip/sebep/son kullanma modeli var veya açıkça kaldırılmış; QR bir rezervasyon semantiği
  icat edemez.

## Handoff

- V1-TBL-004
- V14-QRO-002
