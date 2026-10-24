# V1-RMD-721 - NFC siparişinin yaşam döngüsü ve hata yolları

- Task ID: V1-RMD-721
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-24

## Goal

V1-RMD-602 bulguları B005, B006, B007, B018, B019, B020, B021 ve B026 kapanır. Yaş kısıtlı NFC siparişi süre dolumuyla temizlenir ve masayı kilitli bırakmaz; müşteri ekranı siparişin onay beklediğini söyler; onay anında kimlik kontrolü kayda geçer (yasal zorunluluk doğrulanamadığı için yalnız iz alanı olarak); commit sonrası stok istisnaları müşteriye 500 vermez ve loglanır; aynı gönderim kimliğiyle farklı sepet reddedilir; sipariş numarası çakışmasız ve İstanbul saatiyle üretilir; NFC hata eşlemesi Orders ile aynı ayrımı yapar; anonim uca masa başına hız sınırı yanında istemci başına sınır konur.

## Owned surface

- `plan/v1/remediation/V1-RMD-721-nfc-order-lifecycle-and-errors.md`
- `evidence/V1-RMD-721/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/NfcOrdering/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/PendingOrderConfirmationStore.cs - yalnız kimlik kontrolü izi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/QrOrderExpiryHostedService.cs - yalnız NFC süre dolumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/NfcOrdering/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Confirmation/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız nfc-order hız sınırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/AccessHardeningTests.cs - yalnız nfc-order hız sınırı testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx - yalnız onay bekleme ve kimlik sıfırlama testi
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Yukarıdaki sekiz bulgunun kodu ve gerçek PostgreSQL testleri.

## Out of scope

- Yaş kısıtlı siparişte mutfak fişinin onaydan önce gitmesi PO kararıdır (2026-09-09); değiştirilmez, doküman V1-RMD-724'te hizalanır.

## Dependencies

- V1-RMD-602

## Acceptance evidence

1. Her bulgu için gerçek PostgreSQL veya istemci testi kırmızıdan yeşile döner; `evidence/V1-RMD-721/` altında test günlükleri gerçek exit code ile bulunur.
2. Her düzeltme için mutasyon kanıtı: mutant KILLED, kaynak geri yüklenip doğrulanmış (`mutations.log`).
3. Ürün sahibi senaryosu: yaş kısıtlı NFC siparişi onay beklerken masa kilitlenmez ve süre dolunca temizlenir.
4. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-603
