# V1-RMD-221 - Kurs ateşleme hatası artık yanıltıcı "eşzamanlılık çakışması" demiyor

- Task ID: V1-RMD-221
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) bulduğu **MEDIUM** bulgu:
`Order.FireCourse`'un kendi gerçek hata durumları (kurs zaten
ateşlenmiş / artık Held kalem yok, ya da sipariş artık açık değil)
genel bir `InvalidOperationException` olarak fırlatılıyordu — Host
katmanındaki hata eşleyicisi bunu gerçek bir optimistic-concurrency
çakışmasıyla (`StaleOrderRowVersionException`) AYNI kovaya
("CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından
değiştirildi.") koyuyordu. Aynı masayı iki farklı cihazdan izleyen
garsonlardan biri kursu ateşleyince, diğerinin "tekrar dene" mesajı
alması aslında hiçbir zaman başarılı olamayacak bir isteği tekrar
denemeye teşvik ediyordu.

## Owned surface

- src/Host/Experience/Orders/OrderSubmissionCoordinator.cs (ilgili
  modülün sahipliğinde) — yeni `CourseNotFireableException`;
  `FireCourseAsync`, `order.FireCourse`'un `InvalidOperationException`'ını
  yakalayıp bu yeni tipe sarıyor.
- src/Host/Experience/Orders/OrderManagementEndpoints.cs (aynı modül)
  — hata haritasına yeni, genel `InvalidOperationException`
  dalından ÖNCE gelen bir `CourseNotFireableException` durumu.
- tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
  (aynı modül) — mevcut "ikinci ateşleme reddedilir" testi artık
  gerçek hata kodunu da doğruluyor.

## In scope

1. `CourseNotFireableException`: `Order.FireCourse`'un domain
   mesajını taşıyan, `InvalidOperationException`'dan TÜREMEYEN yeni
   bir tip — hata haritasının onu genel dalla karıştırmaması için
   kasıtlı olarak ayrı.
2. Hata haritasına yeni durum: `409 COURSE_NOT_FIREABLE`, "Bu kurs
   artık ateşlenemez — zaten ateşlenmiş olabilir veya sipariş durumu
   değişti. Listeyi yenileyin." — hem "tekrar dene" tuzağını önlüyor
   hem doğru eylemi (listeyi yenile) söylüyor.
3. Mevcut `RetryingAndAlreadyFiredCourseReplaysWithoutASecondTicket`
   testi artık yanıt gövdesindeki `error.code`'un `COURSE_NOT_FIREABLE`
   olduğunu da doğruluyor.

## Out of scope

- `Order.FireCourse`'un kendisi — hâlâ genel `InvalidOperationException`
  fırlatıyor; bu, aggregate'in kendi kod tabanı genelindeki tutarlı
  kuralı (özel exception tipleri yerine açıklayıcı mesajlı genel
  exception'lar, Host katmanı yorumlar). Yalnızca Host katmanındaki
  YORUMLAMA/eşleme düzeltildi.
- `FireCourseAsync`'in kendi diğer iki `InvalidOperationException`
  fırlatma noktası ("order not found", "kitchen station env var
  missing") — bunlar bu bulgunun kapsamında değildi, dokunulmadı.

## Dependencies

- V1-WTR-025
- V1-RMD-182

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Gerçek Postgres'e karşı `tests/Host/Experience/Orders/TableDraft` →
  79/79 yeşil; revert-and-confirm ile (yeni hata haritası satırı
  geçici olarak kaldırılıp) testin gerçekten kırıldığı (409 yerine
  500) kanıtlandı.

## Handoff

- None
