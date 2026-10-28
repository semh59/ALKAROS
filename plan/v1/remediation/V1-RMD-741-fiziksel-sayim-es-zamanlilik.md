# V1-RMD-741 - Fiziksel sayımda eşzamanlı hareket bakiyeyi bozuyor (739 bulgusu V11-INV-008)

- Task ID: V1-RMD-741
- Status: Done
- Assignee: claude-code-session_019vXeYYCYNP4vpjxAZb7pmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

`RecordPhysicalCountAsync` farkı işlem ve kilit dışında okunan bakiyeden hesaplar. Okuma ile yazma arasında bir satış stok düşerse sayım sonrası bakiye sayılan değer olmaz ve bu sessizce kalır (koruma yalnız negatife bakar). Düzeltme: işlem içinde uygulanan bakiyenin sayılan miktara eşit olduğunu doğrulamak; eşit değilse işlemi geri alıp mevcut 409 eşlemesiyle reddetmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-741-fiziksel-sayim-es-zamanlilik.md`
- `evidence/V1-RMD-741/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/PhysicalCounts/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/PhysicalCounts/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Uygulanan bakiye ile sayılan miktar uyuşmazlığında `PhysicalCountBalanceGuardFailedException` fırlatmak (işlem geri alınır).
- Okuma ile yazma arasına eşzamanlı hareket giren durumu gerçek PostgreSQL ile kanıtlayan test.
- Sınıf yorumundaki "kapsam dışı" ifadesini kodun gerçek davranışına göre düzeltmek.

## Out of scope

- Sayım sırasında stok hareketini engelleyen ürün kuralı (PO kararı).

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Eşzamanlı hareket testi kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-741/` altındadır.
2. Mutasyon kanıtı: doğrulama kaldırılınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739
