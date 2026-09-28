# V1-RMD-418 - Fark raporunun stoğu geri verilmiş (void) kalemin teorik tüketimini saymaması

- Task ID: V1-RMD-418
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-398 stok/reçete denetimi G-02 (Orta): mutfağa gitmiş ama hazırlanmaya başlanmamış bir kalem void edildiğinde
stok geri veriliyor (tüketim hareketi ters kaydediliyor), fakat kabul anında yazılan teorik tüketim kaydı
(`recipe.theoretical_consumption_records`) kalıyor. Gerçek-teorik fark raporu yenmeyen yemeği "beklenen kullanım"
sayıyor ve sahte bir eksik gösteriyor (denetimde 0,4 kg).

Teorik tüketim defteri değiştirilemez (güncelleme/silme tetikleyiciyle yasak, miktar > 0), bu yüzden kayıt silinmez
ya da eksi satır yazılmaz. Bu görev: fark raporu, tüketim hareketi ters kaydedilmiş (stoğu geri verilmiş) sipariş
kalemlerinin teorik tüketimini saymaz. Hazırlanmaya başlanmış kalemin void'i fire sayılır, stoğu geri verilmez; onun
teorik tüketimi sayılmaya devam eder.

## Owned surface

- `plan/v1/remediation/V1-RMD-418-variance-report-skips-voided-consumption.md`
- `evidence/V1-RMD-418/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/MenuInventory/PostgresMenuInventoryReportingService.cs
  (V11-RPT-001 sahipliğinde) — yalnız teorik tüketim alt sorgusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/MenuInventory/MenuInventoryReportingDatabaseTests.cs
  (V11-RPT-001 sahipliğinde) — yeni test

## In scope

- Fark raporunda, `Order` kaynaklı `Consumption` hareketi `Reversal` ile geri alınmış sipariş kaleminin teorik tüketimi
  hariç.

## Out of scope

- Teorik tüketim defterinin şeması ve değiştirilemezliği.
- Diğer fark raporu bulguları (G-03, G-05).

## Dependencies

- V1-RMD-417

## Acceptance evidence

- Kapanışta doldurulacak.

## Handoff

- None
