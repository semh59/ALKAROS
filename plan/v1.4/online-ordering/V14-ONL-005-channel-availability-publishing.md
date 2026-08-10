# V14-ONL-005 - Publish channel availability

- Task ID: V14-ONL-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Tek onaylı kullanılabilirlik projeksiyonundan satılabilir veya kullanılamıyor durumunu etkin çevrimiçi kanallara
yayınlayın.

## Owned surface

- `src/Modules/OnlineOrdering/AvailabilityPublishing/**`, `tests/Modules/OnlineOrdering/AvailabilityPublishing/**`,
  `database/migrations/V14/V14-ONL-005/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kullanılabilirlik event tüketimi, provider azaltma, eş zamanlı güncellemeler, retry, eski/güncel olmayan olayların
  işlenmesi ve sapma tespiti.

## Out of scope

- Stok kesintisi, reçete hesaplama, katalog içeriği ve gelen order kabulü.

## Dependencies

- V14-STK-001
- V14-ONL-004
- V11-INV-007
- V11-MNU-002

## Deliverables

- Onaylanan her kanal için Provider'ye özel kullanılabilirlik yayıncısı.
- Contract, retry, hız sınırı ve eski event testleri.
- Etkin provider'lar için gerçek sandbox kanıtı.

## Acceptance evidence

- Son bölüm geçişi, etkinleştirilen her sandbox kanalına mantıksal olarak bir kez ulaşır; gecikmiş eski olaylar daha
  yeni bir durumun üzerine yazamaz.

## Handoff

- V14-REC-001
- V14-RPT-001
