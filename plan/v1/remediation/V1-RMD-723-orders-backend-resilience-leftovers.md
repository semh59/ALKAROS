# V1-RMD-723 - Orders arka plan dayanıklılığı ve küçük sunucu artıkları

- Task ID: V1-RMD-723
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B012, B015, B016 ve B017 kapanır. Mutfak olayı tüketicisi sırasız olayı geri sarmaz, atılan olayları loglar ve Held kalemi ezmez; masa güncellemesi durum süzgeci taşır; QR/NFC süre dolumu döngüsü bir siparişin hatasında diğerlerini tıkamaz ve siparişi loglar; stok tüketimi işlem bağlantısını kullanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-723-orders-backend-resilience-leftovers.md`
- `evidence/V1-RMD-723/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/** - yalnız ilgili düzeltme
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/** - yalnız yeni test
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Dört bulgunun kodu ve gerçek PostgreSQL testleri. Ek olarak masa üstünde bekleyen Accepted NFC/QR siparişi varken garsonun aynı masaya tur eklemesi kodla yeniden sınanır (doğrulayıcının Çıkarım notu).

## Out of scope

- Havuz ayarı (varsayılan Npgsql) değişmez.
- V1-RMD-602 B011'in sunucu yarısı (taslak kalemlerin faturaya girmesi, Draft siparişin kasaya gönderilmesi) V1-RMD-719'da bırakıldı; taslak kalemin faturalanıp faturalanmayacağı iş kuralı kararı gerektirir, bu görevde yalnız mevcut davranış testle sabitlenir.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-723/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Mutasyon kanıtı: her düzeltme için en az bir mutant KILLED.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
