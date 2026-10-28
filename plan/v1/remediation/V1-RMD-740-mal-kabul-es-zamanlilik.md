# V1-RMD-740 - Mal kabulde eşzamanlı çift stok artışı (739 bulgusu V11-PUR-001, V11-RMD-002)

- Task ID: V1-RMD-740
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

`ReceiveGoodsAsync` siparişi ve satır miktarlarını kilitsiz okur, sonra satırın alınan miktarını mutlak değerle yazar. Aynı siparişe iki eşzamanlı farklı fişle mal kabul yapılırsa her iki istek de yazılır ve stok iki kez artar. Düzeltme: işlem içinde sipariş satırlarını kilitleyip okunan alınan miktarın değişmediğini doğrulamak; değişmişse kabulü reddetmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-740-mal-kabul-es-zamanlilik.md`
- `evidence/V1-RMD-740/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/OrdersAndReceipts/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/OrdersAndReceipts/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Mal kabul işleminde sipariş satırlarını `FOR UPDATE` ile kilitlemek ve işlem öncesi okunan alınan miktarla karşılaştırmak; fark varsa istisna (409 eşlemesi mevcut durum istisnasıyla).
- Eşzamanlı iki kabulün tek stok artışı bıraktığını gerçek PostgreSQL ile kanıtlayan test.

## Out of scope

- Mal kabul davranışının başka değişikliği; ürün kararı gerektiren kalemler.

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Eşzamanlı iki kabul testi kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-740/` altındadır.
2. Mutasyon kanıtı: kilit/karşılaştırma kaldırılınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
