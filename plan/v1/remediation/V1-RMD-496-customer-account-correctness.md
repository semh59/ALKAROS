# V1-RMD-496 - Müşteri hesabı doğruluk düzeltmeleri (arama kapsamı, tutar ölçeği)

- Task ID: V1-RMD-496
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bağımsız taramanın bulduğu iki müşteri hesabı hatasını kapatmak: 500'den eski müşterinin listede ve aramada hiç görünmemesi (borç yazılamaz), ve hesaba yazma tutarının iki ondalığı aşan ölçekle kabul edilip kayıtla ham tutar arasında tutarsızlık yaratması.

## Owned surface

- `plan/v1/remediation/V1-RMD-496-customer-account-correctness.md`
- `evidence/V1-RMD-496/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/CustomerAccounts/CustomerAccountsStore.cs — yalnız arama kapsamı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/BillCharges/AccountChargeContracts.cs — yalnız tutar ölçeği doğrulaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CustomerAccounts/CustomerAccountsHttpTests.cs ve tests/Modules/CustomerAccounts/BillCharges/AccountChargeHandlerTests.cs — yalnız bu görevin testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Arama terimi verildiğinde müşteriler en yeni 500 ile sınırlı kalmaz (en çok 5000 profil taranır); terimsiz liste en yeni 500 olarak kalır.
- Hesaba yazma tutarı en çok iki ondalık olabilir; fazlası doğrulama hatasıyla reddedilir (yuvarlanıp yazılmaz).

## Out of scope

- Profil alanlarında sunucu tarafı şifreli arama dizini; müşteri anonimleştirme akışları.

## Dependencies

- V15-KVK-002

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-496/` altındadır.

## Handoff

- None
