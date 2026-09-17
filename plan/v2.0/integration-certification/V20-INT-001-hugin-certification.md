# V20-INT-001 - Certify Token/Beko integration

- Task ID: V20-INT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- EXT:TOKEN-DEVELOPER-PORTAL
- CORR:C98

## Goal

**Retarget notu (2026-09-17, `V0-GOV-064`/CORR:C98):** hedef cihaz Hugin
T300 değil, Token/Beko (300 TR / X30 TR). Task ID değişmedi.

Onaylanan Token/Beko model/ürün yazılımı/protokol kombinasyonunu mali satış, retry, toplam ve arıza senaryolarına göre
onaylayın.

## Owned surface

- `release/evidence/integrations/token/**`
- Bu görev Token adapter kodunu değiştiremez.

## In scope

- Cihaz kimliği, donanım yazılımı/protokol kanıtları, satış/geri ödeme/iptal durumları, timeout/retry, terminal-toplam
  mutabakatı ve düzeltilmiş ham transkriptler.

## Out of scope

- Adapter uygulaması, mali hukuki yorum ve diğer cihazların sertifikasyonu.

## Dependencies

- V13-HUG-001
- V13-HUG-002
- V13-HUG-003
- V13-HUG-004
- V13-FSC-003

## Deliverables

- Cihaz test matrisi, redacted transcriptler ve imzalı sertifikasyon sonucu.

## Acceptance evidence

- Onaylanan her zorunlu senaryo, adı geçen fiziksel cihazı/ürün yazılımını aktarır; hiçbir retry açıklanamayan yinelenen
  bir mali işlem üretmez.
- `V13-FSC-003` tarihli `NotApplicable` ise adisyon lifecycle bu certification'a dahil edilmez; Token payment ve
  terminal
  total senaryoları yine kanıtlanır.
- `V13-HUG-004` kanıtlı `NotApplicable` ise terminal totals senaryoları certification kapsamına dahil edilmez; Token
  payment senaryoları yine kanıtlanır.
- NotApplicable koşulu: `GATE-V13-FSC-STRATEGY` tarihli branch kararı Token/Beko'yu dışlarsa bu task kanıtlı `NotApplicable`
  olarak kapanır (karar kaydı + `V13-FSC-003`/`V13-FSC-004` durum kanıtıyla).

## Handoff

- V20-GAT-002
