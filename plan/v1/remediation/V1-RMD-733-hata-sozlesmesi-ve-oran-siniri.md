# V1-RMD-733 - Hata sözleşmesi ve terminal oran sınırı (603 B023, B029)

- Task ID: V1-RMD-733
- Status: Done
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B023, B029: GRANT_DENIED İngilizce ve zarfsız; tüm iade ve bölme doğrulama hataları tek genel mesaj; sipariş durumu çakışması 400; fiş 404 ve bozuk gövde zarfsız; NOT_FOUND mesajı hep hesap; Billing uçlarında terminal bazlı oran sınırı yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-733-hata-sozlesmesi-ve-oran-siniri.md`
- `evidence/V1-RMD-733/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Receipt.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Türkçe hata zarfı ve nedene özgü kodlar; 409 UNSUPPORTED_ORDER_STATE; fiş ve gövde hatalarında zarf; terminal-read/terminal-write politikaları; 401/403/404 tablo testleri.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-733/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
