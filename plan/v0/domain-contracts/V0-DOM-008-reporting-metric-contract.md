# V0-DOM-008 - Define reporting metric contracts

- Task ID: V0-DOM-008
- Status: Done
- Assignee: codex-v0-dom-008
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Rapor kodu yazılmadan önce PDF `II.10` kapsamındaki her raporun formula, granularity ve source table sözleşmesini
tanımlamak.

## Owned surface

- `docs/domain/reporting-metrics.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Satış, ürün/kategori, garson/table, satış oranı, porsiyon, atık, cash, payment karışımı, ödemeler, yaşlanma,
  mutabakat, yazıcı ve yedekleme ölçümleri.

## Out of scope

- Kontrol paneli düzeni, BI aracı ve dışa aktarma formatı.

## Dependencies

- V0-DAT-004
- V0-CMP-002

## Deliverables

- V0-DOM-008 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Her ölçümde granularity, filtreler, saat dilimi/iş tarihi, source-of-truth ve reconciliation total bulunur; tanımsız
  terim `Blocked` kalır.

## Handoff

- V1-RPT-001
- V11-RPT-001
- V12-RPT-001
- V13-RPT-001
- V14-RPT-001
- V15-RPT-001
