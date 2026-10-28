# V1-RMD-742 - Bakiye yeniden kurma önce siliyor, büyük defterde hata veriyor (739 bulgusu V11-INV-002)

- Task ID: V1-RMD-742
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

`RebuildAllBalancesAsync` önce bakiye tablosunu siler, sonra her konumun hareketlerini `GetByLocationAsync` ile okur. O sorgu 5000 satırı aşınca `InvalidOperationException` fırlatır; silme çoktan yapıldığı için bakiyeler boş kalır ve yoğun bir konumda yeniden kurma hiçbir zaman başarılı olamaz. Düzeltme: bakiye toplamlarını tek SQL toplama sorgusuyla (kalem + konum bazında) okumak ve tabloyu ancak toplamlar hazır olunca sıfırlayıp yazmak.

## Owned surface

- `plan/v1/remediation/V1-RMD-742-bakiye-yeniden-kurma-guvenligi.md`
- `evidence/V1-RMD-742/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/BalanceProjection/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/MovementLedger/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- `IStockMovementRepository` üzerine toplam okuma yöntemi eklemek (Postgres uygulaması SQL `SUM`, sahte sınıflar test güncellemesi).
- Yeniden kurmayı okuma-önce, silme-sonra sırasına almak; 5000 satırı aşan defterde de başarılı olduğunu kanıtlamak.

## Out of scope

- Yeniden kurmayı üretimde bir uç noktaya bağlamak (ayrı karar).
- `ReplayBalanceForItemAndLocationAsync` sınırı.

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. 5000'den fazla hareketli konumda yeniden kurma testi kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-742/` altındadır.
2. Mutasyon kanıtı: sıra geri çevrilince veya toplam yanlışlanınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
