# V20-INT-003 - Certify Yemeksepeti integration

- Task ID: V20-INT-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- EXT:YSP-PARTNER-2.0.2

## Goal

Gelen siparişler ve giden status, katalog ve kullanılabilirlik işlemleri için onaylanmış Yemeksepeti contract'yi
onaylayın.

## Owned surface

- `release/evidence/integrations/yemeksepeti/**`
- Bu görev online-order adapter kodunu değiştiremez.

## In scope

- İmzalı webhook/replay, order normalleştirme, eşleme, kabul etme/reddetme/iptal etme, katalog yayınlama,
  kullanılabilirlik yayınlama, retry ve hız sınırı durumları.

## Out of scope

- Onaylanmayan kanalların eklenmesi, provider contract müzakere ve stok hesaplama.

## Dependencies

- V0-YSP-001
- V14-ONL-001
- V14-ONL-002
- V14-ONL-003
- V14-ONL-004
- V14-ONL-005

## Deliverables

- Gerçek sandbox test matrisi, redacted transcriptler ve sapma raporu.

## Acceptance evidence

- Zorunlu sandbox senaryoları geçer ve dahili/provider order, katalog ve kullanılabilirlik durumları açıklanamayan
  kopyalar veya sapmalar olmadan uzlaştırılır.

## Handoff

- V20-GAT-002
