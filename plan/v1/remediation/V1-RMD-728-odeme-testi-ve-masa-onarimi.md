# V1-RMD-728 - Kırmızı ödeme testi ve masa onarımı (603 B003, B038, N1, N2)

- Task ID: V1-RMD-728
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B003, B038, N1, N2: PaymentTenderHttpTests tohumu table_sent_checks satırı yazmadığı için masa onarımı siparişi kapalı sayıp masayı Available yapıyor ve test kırmızı; onarım servisi masa durumunu da eziyor (yorumuyla çelişkili); bölme allocation sırası eşitlikte belirsiz ve BillingSplitHttpTests sıraya bağımlı.

## Owned surface

- `plan/v1/remediation/V1-RMD-728-odeme-testi-ve-masa-onarimi.md`
- `evidence/V1-RMD-728/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/SplitDesign/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Tables/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Tables/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Test tohumunun gerçek gönderim yoluyla uyumu; onarım servisinin masa durumunu yazıp yazmayacağı kodla belirlenir ve yorumla hizalanır; allocation sorgusuna eşitlik ayırıcısı.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-728/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
