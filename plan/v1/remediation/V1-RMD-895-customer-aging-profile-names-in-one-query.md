# V1-RMD-895 - Yaşlandırma raporunda müşteri adları tek sorguyla okunur (müşteri başına profil okuması yok)

- Task ID: V1-RMD-895
- Status: InProgress
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

`GET /api/v1/management/reports/customer-aging/` her müşteri için `CustomerAccountsStore.GetAsync` çağırır: müşteri başına iki sorgu (hesap satırı ve şifreli profil). 883 raporu veritabanında toplulaştırdı ve müşteri sayısı üst sınırı 100.000'dir; ad okuması hâlâ N+1'dir. Profil deposuna toplu okuma (`GetManyAsync`, tek sorgu, yalnız istenen kimlikler, anonimleştirilmiş profil dahil rol projeksiyonu aynen) eklenir; `CustomerAccountsStore` ad sözlüğü verir; rapor uç noktası adları tek çağrıyla alır. Anonimleştirilmiş veya profili olmayan müşteri eskisi gibi "Profili okunamayan müşteri" olarak görünür.

## Owned surface

- `evidence/V1-RMD-895/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/CustomerData/Profiles/ICustomerProfileStore.cs — toplu okuma
  - src/Modules/CustomerData/Profiles/PostgresCustomerProfileStore.cs — toplu okuma sorgusu
  - src/Host/Experience/CustomerAccounts/CustomerAccountsStore.cs — ad sözlüğü
  - src/Host/Experience/CustomerAging/ReceivablesAgingEndpoints.cs — tek çağrı
  - tests/Modules/CustomerData/Profiles/PostgresCustomerProfileStoreTests.cs — toplu okuma testi
  - tests/Host/Experience/CustomerAccounts/CustomerAgingHttpTests.cs — uç nokta testi
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: uç nokta çok müşteriyle çağrıldığında profil deposuna tek toplu çağrı gider, müşteri başına tek okuma yoktur; adlar, anonimleşmiş ve eksik profil metni önceki gibidir
2. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Kasa müşteri listesi (`ListAsync`) ve arama; rapor sonucu ve toplulaştırma sorgusu

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
