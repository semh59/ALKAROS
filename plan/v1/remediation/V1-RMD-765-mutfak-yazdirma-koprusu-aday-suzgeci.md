# V1-RMD-765 - Mutfak yazdırma köprüsü yalnız basılabilir biletleri aday alır

- Task ID: V1-RMD-765
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulgusu V1-RMD-130: `KitchenPrintDispatchHostedService.FindTicketsAwaitingPrintJobsAsync` `created_at` sırasıyla ilk 50 bileti seçer; süzgeçte yalnız "iptal değil" ve "yazdırma işi yok" vardır. İki sonuç:

1. Etkin yazıcısı olmayan bir istasyonun bileti hiç yazdırma işi almadığı için her turda aynı en eski 50 bilet aday kalır; 50 böyle bilet birikince yazıcısı olan istasyonların yeni biletleri hiç köprülenmez.
2. Sonradan bir istasyona yazıcı tanımlanınca o istasyonun geçmişteki hazır (`Ready`) biletleri de toplu fiziksel baskıya girer.

Aday sorgusu yalnız etkin yazıcısı olan istasyonların, hâlâ hazırlanmakta olan (`Queued`, `Accepted`, `Preparing`) biletlerini seçer. Bilet durum değerleri `kitchen.kitchen_tickets` kısıtından alınır.

## Owned surface

- `plan/v1/remediation/V1-RMD-765-mutfak-yazdirma-koprusu-aday-suzgeci.md`
- `evidence/V1-RMD-765/**`
- Sınırlı ek: src/Host/Experience/KitchenOperations/KitchenPrintDispatchHostedService.cs
- Sınırlı ek: tests/Host/Experience/KitchenOperations/KitchenPrintDispatchHostedServiceTests.cs

## In scope

- Aday sorgusuna etkin yazıcı varlığı ve hazırlanan bilet durumu süzgeci.
- Gerçek PostgreSQL testleri: (a) yazıcısız istasyonun 50 eski bileti, yazıcılı istasyonun yeni biletinin köprülenmesini engellemez; (b) hazır bilet köprülenmez.

## Out of scope

- Köprü aralığı, parti boyutu, yazdırma işi yaşam döngüsü ve kurtarma (V1-KIT-003/004).
- Bilet yaşına göre zaman penceresi (bir ürün kuralı olur).

## Dependencies

- V1-RMD-130

## Acceptance evidence

1. Düzeltmeden önce iki yeni test kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `ALKAROS.Host.Experience.KitchenOperations.Tests` (gerçek PostgreSQL) exit 0 geçer.
3. Mutasyon: yazıcı süzgeci ve durum süzgeci ayrı ayrı kaldırılınca ilgili test kırmızı olur; dosya hash'i geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None
