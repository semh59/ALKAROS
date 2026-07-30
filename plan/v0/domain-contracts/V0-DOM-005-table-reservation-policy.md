# V0-DOM-005 - Define table reservation policy

- Task ID: V0-DOM-005
- Status: Done
- Assignee: codex-v0-dom-005
- Work type: decision
- Surface state: Planned

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

## Deliverables

- V0-DOM-005 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Ayrılmış durumun kalıcı bir sahip/sebep/son kullanma modeli var veya açıkça kaldırılmış; QR bir rezervasyon semantiği
  icat edemez.

## Handoff

- V1-TBL-004
- V14-QRO-002
