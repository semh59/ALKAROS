# V1-RMD-186 - Inventory kodunda İngilizce yorum içinde kalmış Türkçe sızıntı

- Task ID: V1-RMD-186
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-179/180'i kapatırken çalıştırılan `consistency_audit.py`'nin
gösterdiği, bu oturumdan önce var olan tek ihlali kapatır (daha önce
V1-RMD-177/178'in kendi Acceptance evidence bölümlerinde de "sahip
olunan yüzeyin dışında" diye not edilmişti, hiçbir görev onu hiç
sahiplenmemişti):

`InventoryAdjustmentService.cs:96`'daki yorum, İngilizce bir cümlenin
sonuna parantez içinde Türkçe bir çeviri eklemişti
(`// Non-negative outcome invariant ("olumsuz olmayan sonuç")`).
AGENTS.md'nin "kod, semboller ve testler İngilizce" kuralına aykırı.
Parantez içindeki Türkçe gloss kaldırıldı; yorum artık salt İngilizce.

## Owned surface

- `plan/v1/remediation/V1-RMD-186-inventory-comment-turkish-leak.md` (yeni)
- Sınırlı ek:
  - src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs
    (Inventory modülü sahipliğinde) — tek satırlık yorum düzeltmesi,
    davranış değişikliği yok.

## Out of scope

- Yok — tek satırlık, davranışsız bir yorum düzeltmesi.

## Dependencies

- None

## Acceptance evidence

- `dotnet build src/Modules/Inventory/ALKAROS.Inventory.csproj -c Debug`
  → 0 uyarı, 0 hata.
- `python tools/consistency-audit/consistency_audit.py` → `consistency-audit: clean`
  (önceki tek ihlal buydu, artık yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.

## Handoff

- None
