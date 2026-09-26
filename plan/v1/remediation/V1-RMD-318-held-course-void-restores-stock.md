# V1-RMD-318 - Bekleyen (Held) kurs iptal edilirse stoğu geri verilmiyordu

- Task ID: V1-RMD-318
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K6 bulgusu: `SentItemVoidStore.VoidAsync`'in stok iadesi kontrolü yalnız `item.KitchenState == KitchenState.Sent` kontrolü yapıyordu. `KitchenState.Held` (bir turun henüz mutfağa çağrılmamış sonraki kursu) bu kontrolden geçemiyordu — ama `OrderSubmissionStockDispatcher` stok tüketimini `IsActive` bayrağına göre yapıyor (`KitchenState`'e göre değil), yani `Order.FireRound` bir Held kalemi de Active yapıp aynı anda stoğunu tüketiyor. Hiç mutfağa çağrılmamış (Held) bir kursun iptalinde tükettiği stok kalıcı olarak envanterden düşük kalıyordu — gerçek mali/stok kaybı.

## Owned surface

- `plan/v1/remediation/V1-RMD-318-held-course-void-restores-stock.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs

## In scope

1. `VoidAsync`'in stok iadesi koşulu `KitchenState.Sent` VEYA `KitchenState.Held`'i kapsayacak şekilde genişletilir.
2. Yanlış/eski yorum ("bu store'un kendi ön koşulu Sent/Preparing/Ready garantiliyor") düzeltilir — gerçekte Held de kabul ediliyor (yalnız NotSent/Served/Cancelled reddediliyor).

## Out of scope

- Fatura atık (waste) dönüşümü (`ApplyBillWasteConversionAsync`) — `item.KitchenState`'e bakmadan zaten her voidlenen sent kalem için çalışıyor; bu görevin bulgusu yalnız stok iadesiyle ilgili, para/atık tarafı ayrı bir inceleme gerektirir.
- `Order.FireCourse`'un mimarisi — K5'te (V1-RMD-317) ele alındı.

## Dependencies

- None

## Acceptance evidence

Host testleri (UTF8 Postgres 18), gerçek bir HTTP sunucusuna karşı: yeni test `VoidingAHeldLaterCourseItemBeforeItIsEverCalledInRestoresItsStock` — bir `KitchenState.Held` kalem, gerçekten tüketilmiş stok hareketiyle (`SeedConsumedStockForItemAsync`) seed edilir; void isteği sonrası `StockRestored=true` ve gerçek eldeki miktar (`GetOnHandQuantityAsync`) tüketim öncesi değere geri döner (9 → 10). `ALKAROS.Host.Experience.Orders.VoidSent.Tests` 15/15 (1 yeni), regresyon yok.

Mutasyon kontrolü: `stockRestored` koşulu geçici olarak eski hâline (`== KitchenState.Sent`) döndürüldü — yeni test gerçekten kırmızı oldu (`Expected: True, Actual: False`); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None
