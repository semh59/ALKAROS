# V20-INT-001 - Certify Hugin integration

- Task ID: V20-INT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- EXT:GIB-HUGIN-T300
- EXT:HUGIN-PC-LINK-V1
- EXT:HUGIN-CLOUD-LINK-V1-T300

## Goal

Onaylanan Hugin model/ürün yazılımı/protokol kombinasyonunu mali satış, retry, toplam ve arıza senaryolarına göre
onaylayın.

## Owned surface

- `release/evidence/integrations/hugin/**`
- Bu görev Hugin adapter kodunu değiştiremez.

## In scope

- Cihaz kimliği, donanım yazılımı/protokol kanıtları, satış/geri ödeme/iptal durumları, timeout/retry, terminal-toplam
  mutabakatı ve düzeltilmiş ham transkriptler.

## Out of scope

- Adapter uygulaması, mali hukuki yorum ve diğer cihazların sertifikasyonu.

## Dependencies

- V12-HUG-001
- V12-HUG-002
- V12-HUG-003
- V12-HUG-004
- V12-FSC-003

## Deliverables

- Cihaz test matrisi, redacted transcriptler ve imzalı sertifikasyon sonucu.

## Acceptance evidence

- Onaylanan her zorunlu senaryo, adı geçen fiziksel cihazı/ürün yazılımını aktarır; hiçbir retry açıklanamayan yinelenen
  bir mali işlem üretmez.
- `V12-FSC-003` tarihli `NotApplicable` ise adisyon lifecycle bu certification'a dahil edilmez; Hugin payment ve terminal
  total senaryoları yine kanıtlanır.

## Handoff

- V20-GAT-002
