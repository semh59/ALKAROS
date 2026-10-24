# V1-RMD-726 - Sıfır tutarlı hesap kapanışı ve yüzde yüz indirim (603 B001)

- Task ID: V1-RMD-726
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B001: ödenecek tutarı sıfır olan hesap (yüzde yüz indirim veya tümü ikram) hiçbir tahsilatla Paid olamıyor, ekran 'Hesap Ödendi' derken sunucuda hesap Open ve sipariş açık kalıyor. Sıfır tutarlı hesabın kapanışı tanımlanır ve indirim/ikram sonrası tetiklenir; ekranın 'ödendi' durumu sunucu durumuyla eşitlenir; tam tutar indirimi önizleme ve ikinci onay ister.

## Owned surface

- `plan/v1/remediation/V1-RMD-726-sifir-tutarli-hesap-kapanisi.md`
- `evidence/V1-RMD-726/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Sıfır tutarlı hesabın kapanışı BillPaymentClosureCalculator ve TryCloseAsync üzerinden; indirim yolunun kapanışı tetiklemesi; Cashier indirim önizlemesi ve onay. Varsayılan karar: ödenecek sıfır ise ödeme şartı aranmaz, hesap Paid olur ve sipariş tamamlanır.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-726/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
