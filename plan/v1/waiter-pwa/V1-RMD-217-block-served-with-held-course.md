# V1-RMD-217 - Bekletilen bir kurs varken hesap Served'e geçemesin

- Task ID: V1-RMD-217
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **HIGH** bulgu:
`GarsonFeature.CourseManagement` servis ortasında kapatılırsa (ya da
başka bir çağıran `FireCourse`'u hiç çağırmazsa), o anda hâlâ
`KitchenState.Held` durumunda bir kurs kalemi taşıyan bir hesap
`Served`/`Completed`'e kadar hiçbir kontrolden geçmeden ilerleyebiliyordu
— müşterinin sipariş ettiği kurs mutfağa hiç gitmeden hesap
kapanabiliyordu, ve `Served`'e ulaştıktan sonra `FireCourse` zaten
`IsOpenCheck` koruması yüzünden bir daha hiç çağrılamıyordu.

## Owned surface

- src/Modules/Orders/OrderAggregate/Order.cs (ilgili modülün
  sahipliğinde)
- tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs (aynı modül)

## In scope

1. `CanTransitionTo(OrderState.Served)`: artık `Status is
   OrderState.Ready` YETMİYOR, ek olarak `Items.All(item =>
   item.KitchenState != KitchenState.Held)` de gerekiyor — bir hesap,
   her kursu gerçekten ateşlenmeden (ya da iptal edilmeden) Served
   olamaz. İptal edilmiş bir kalemin `KitchenState`'i zaten `Cancelled`
   olduğu için bu kontrolü etkilemiyor.
2. İki yeni test: bekletilen bir kurs varken `Served`'e geçiş
   engelleniyor; her kurs ateşlendikten sonra geçiş serbest.

## Out of scope

- `GarsonFeature.CourseManagement`'ın kendi kapatma mantığı — "aktif
  Held siparişi varken kapatmayı engelle" gibi bir ön-kontrol ayrı bir
  görev olabilir; bu görev yalnız sonuçtaki veri kaybını (kalemin
  sessizce hiç gitmemesi) önlüyor.
- Hata mesajının kullanıcıya nasıl yansıdığı — genel `InvalidOperationException`
  mesaj eşlemesi V1-RMD-221'in kapsamında.

## Dependencies

- V1-WTR-025

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Modules/Orders/OrderAggregate` → 128/128 yeşil
  (2 yeni test dahil); revert-and-confirm ile gerçekten kırılıp
  doğrulandı.
- Regresyon taraması: `tests/Modules/Orders/SubmitOrder`,
  `tests/Host/Experience/Orders/{TableDraft,Void,VoidSent,Comp}` → tümü
  yeşil.

## Handoff

- None
