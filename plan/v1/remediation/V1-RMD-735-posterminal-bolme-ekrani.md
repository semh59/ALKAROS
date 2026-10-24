# V1-RMD-735 - PosTerminal bölme ekranı ve ürün bazlı bölme bağlantısı (603 B034, B035)

- Task ID: V1-RMD-735
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B034, B035: BillSplitWorkspace 409 sonrası kurtarma, boş hücre, boş sahip listesi, çift yükleme, label hatası, metinler ve jargon; ürün bazlı bölme tasarımının tahsilat ekranına bağlanması ve Semih senaryosunun uçtan uca sağlanması.

## Owned surface

- `plan/v1/remediation/V1-RMD-735-posterminal-bolme-ekrani.md`
- `evidence/V1-RMD-735/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing-split/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- BillSplitWorkspace düzeltmeleri; kaydedilen dağılımdan kişi başı tutar önerisi; indirimden sonra ürün bazlı bölme davranışının kullanıcıya açık sunulması; senaryo testi.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-735/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
