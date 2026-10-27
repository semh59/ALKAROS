# V1-RMD-736 - Durum makinesi, kapanış yeniden denemesi ve ölü kod (603 B019, B020, B030, B040)

- Task ID: V1-RMD-736
- Status: InProgress
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B019, B020, B030, B040: iade sonrası Paid->Cancelled ham SQL ve geçiş matrisi çelişkisi; Reopened ve PartiallyPaid atanmıyor; hesap kapatma best effort ve yeniden deneme yok; üretimde çağrılmayan ama açık kod (RemoveAsync vb.); iptal 499, kilit hatası eşlemesi, ikinci bağlantı.

## Owned surface

- `plan/v1/remediation/V1-RMD-736-durum-makinesi-ve-kapanis.md`
- `evidence/V1-RMD-736/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/**
- `src/Host/Experience/Billing/BillClosureSweeper.cs`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Geçiş matrisinin iade sonucunu içermesi; kapanışı yeniden deneyen süpürücü; kullanılmayan metotların silinmesi; OperationCanceled 499; 40P01/55P03 eşlemesi.

## Out of scope

- Masa işaretçisi yazımında yutulan hatanın günlüğü ve Billing Host testi V1-RMD-732 kapsamındadır; bu görev ona dokunmaz.
- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-736/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
