# V1-RMD-748 - Stok hareketi iptali bakiyeyi eksiye çekince ham veritabanı hatası fırlatıyor (739 bulgusu V11-INV-003)

- Task ID: V1-RMD-748
- Status: Done
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

İşlemli ReverseMovementAsync, bakiyeyi korumasız ApplyOnHandDeltaAsync ile günceller. Bir 'In' hareketin (örn. mal kabul) iptali bakiyeyi eksiye çekecekse ck_stock_balances_on_hand_non_negative ihlali ham PostgresException olarak yayılır; tipli bir ret yoktur. Düzeltme: korumalı TryApplyGuardedOnHandDeltaAsync kullanmak ve null dönünce StockMovementReversalException altı tipli bir istisna (ReversalWouldMakeStockNegativeException) fırlatmak; çağıranın işlemi geri alınır, SentItemVoidStore bu taban sınıfı zaten yakalar.

## Owned surface

- `plan/v1/remediation/V1-RMD-748-hareket-iptali-eksi-bakiye.md`
- `evidence/V1-RMD-748/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/MovementReversal/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/MovementReversal/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- İşlemli ReverseMovementAsync içinde korumalı uygulama ve tipli istisna
- Mal kabul iptalinin bakiyeyi eksiye çekeceği durumda tipli ret ve değişmeyen bakiye testi

## Out of scope

- İşlemsiz ReverseMovementAsync aşırı yüklemesinin atomikliği (üretimde çağıranı yok, ayrı karar)

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-748/` altındadır.
2. Mutasyon kanıtı: düzeltme geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
