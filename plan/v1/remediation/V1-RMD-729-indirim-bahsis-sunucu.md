# V1-RMD-729 - İndirim ve bahşiş sunucu sertleştirme (603 B006, B008–B011, B014, B024)

- Task ID: V1-RMD-729
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-603 B006, B008, B009, B010, B011, B014, B024: null/boş idempotency anahtarı tekrar sayılıyor, aynı anahtar farklı gövde 200 dönüyor; durum kilitten önce okunuyor; satır sürümü artmıyor; grant onaylayanı kaydedilmiyor; değer üst sınırı ve not uzunluğu yok; yetkisiz rolde geçersiz değerle grant açılabiliyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-729-indirim-bahsis-sunucu.md`
- `evidence/V1-RMD-729/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/**
- `database/migrations/V1/V1-RMD-729/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Anahtar doğrulaması ve yük karşılaştırma (409); kilit sonrası yeniden okuma; ayarlamada hesap sürümünü artırma ve bölme çakışmasını 409'a eşleme; grant onaylayan ve grant id kaydı; tutar üst sınırı ve not uzunluğu (400); grant öncesi ön doğrulama.

## Out of scope

- Ürün kararı gerektiren kalemler (PO soruları) sorulmadan değiştirilmez.
- Bu görevin başlığında adı geçmeyen bulguların düzeltmesi.

## Dependencies

- V1-RMD-603

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-729/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Her bulgu kodla yeniden doğrulanır; gerçek olmayanlar `README.md` içinde kod kanıtıyla kapatılır.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-604
