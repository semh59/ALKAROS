# V1-RMD-737 - Billing test boşlukları (603 B005, B006, B037)

- Task ID: V1-RMD-737
- Status: InProgress
- Assignee: claude-code-session_019auoHryukF9ryWZPtnjznR
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B005, B006, B037: mutasyonla ortaya çıkan test boşlukları: AdjustedTaxTotal ve AdjustedDiscountTotal değer testleri, özel bölme eksik toplam, bahşiş durum ve özellik kapısı, iade sınırları ve ret, kısmi iade sonrası iptal, Unknown/ReconciliationRequired iptal, kapanış servisi, sıfır birim fiyat, yarım kuruş yuvarlama, uçlar için 401/403/404 testleri, eşzamanlılık testleri.

## Owned surface

- `plan/v1/remediation/V1-RMD-737-test-bosluklari.md`
- `evidence/V1-RMD-737/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/StaticApps/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Yalnız test; hayatta kalan 32 mutantın ilgili olanları öldürülür (test-kalitesi mutants.tsv). Üretim kodu değişirse ilgili düzeltme görevine bildirilir.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

- Hesap kapatma süpürücüsü (`BillClosureSweeper.cs`) V1-RMD-736 kapsamındadır; bu görev ona dokunmaz.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-737/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
