# V1-RMD-332 - Void/comp/kabul/red işlemlerinin denetim kaydı artık ana işlemle aynı transaction'da

- Task ID: V1-RMD-332
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Void/comp/accept/reject audit kaydı ana
işlemle aynı transaction'da değil" (`ItemExceptionHandler.cs:126-136,275-285`,
`PendingOrderConfirmationStore.cs:129-136,181-188`). Doğrulandı: her dört metodda (`VoidItemAsync`,
`ApplyComplimentaryAsync`, `AcceptAsync`, `RejectAsync`) sipariş/kalem yazısı bir bağlantı+transaction'da commit
ediliyor, `audit.audit_events` satırı ise sonrasında AYRI bir bağlantıda ekleniyordu. İki commit arasında süreç
çökerse (veya audit ekleme bir kısıtlama/bağlantı hatasıyla başarısız olursa), sipariş kalıcı olarak
void/comp/kabul/red edilmiş görünür ama PDF:II.9/III.24'ün zorunlu kıldığı denetim izi hiç yoktur — sessiz bir
boşluk.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/ItemExceptions/ItemExceptionsTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Confirmation/OrderManagementConfirmationHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Confirmation/OrderManagementConfirmationTestDatabase.cs
- `plan/v1/remediation/V1-RMD-332-item-exception-and-confirmation-audit-atomicity.md`

## In scope

1. `ItemExceptionHandler.VoidItemAsync`/`ApplyComplimentaryAsync`: kendi bağlantı+transaction'ını açar,
   `IOrderRepository`'nin zaten var olan transaction-alan `SaveAsync` aşırı yüklemesini ve artık aynı
   bağlantı/transaction'ı alan `AppendAuditAsync`'i çağırır, sonra tek seferde commit eder.
2. `PendingOrderConfirmationStore.AcceptAsync`/`RejectAsync`: zaten açık olan (stok tüketimi/masa serbest bırakma
   için kullanılan) transaction'a audit eklemesini de dahil eder — commit'ten SONRA yapılan ayrı çağrı kaldırıldı.
3. Gerçek bir Postgres kısıtlaması (audit tablosunun kendi `correlation_id VARCHAR(128)` sütunu — enjekte edilmiş
   bir hata değil) ile audit eklemesinin GERÇEKTEN başarısız olduğu, ve bunun sonucunda ana yazının da geri
   alındığı bir test.

## Out of scope

1. `audit.audit_events`'e yazan diğer tüm çağrı siteleri (kod tabanında onlarca) — bu görev yalnızca denetimin
   isim verdiği iki dosyayı (dört metodu) kapsıyor. Aynı desenin başka yerlerde de tekrarlanıp tekrarlanmadığının
   sistematik taranması ayrı bir görev.
2. `PendingOrderConfirmationStore` için "audit eklemesi gerçekten başarısız olursa ana yazı geri alınır" iddiasını
   `ItemExceptionHandler`'daki gibi GERÇEK bir Postgres kısıtlama ihlaliyle kanıtlamak — bu metotların audit
   satırındaki hiçbir alan (event_name/correlation_id sabit, reason TEXT sınırsız) istemci girdisinden doğal
   olarak bir kısıtlama ihlaline yol açacak şekilde kontrol edilemiyor. Bu dosya için kanıt, başarılı yoldan sonra
   audit satırının gerçekten var olduğunu doğrulayan pozitif testlerle ve `ItemExceptionHandler`'da AYNI
   mekanizmanın (aynı bağlantı/transaction paylaşımı) gerçek bir arıza enjeksiyonuyla kanıtlanmış olmasıyla
   sınırlı.

## Dependencies

- None

## Acceptance evidence

- `tests/Modules/Orders/ItemExceptions/ALKAROS.Orders.ItemExceptions.Tests.csproj`: 22/22 test geçti (2 yeni test
  dahil). `WhenTheAuditInsertItselfFailsTheDomainWriteInsideTheSameTransactionIsRolledBackToo`: 128 karakterlik
  sütun sınırını aşan bir `CorrelationId` ile audit eklemesi gerçek bir `PostgresException` (value too long) ile
  başarısız oluyor VE kalemin durumu `Active` kalıyor, satır sürümü değişmiyor, hiç audit satırı yazılmıyor —
  eski koda karşı (mutasyon kontrolü, aşağıya bkz.) bu test kalemin yanlışlıkla `Cancelled` kaldığını yakaladı.
- `tests/Host/Experience/Orders/Confirmation/ALKAROS.Host.Experience.Orders.Confirmation.Tests.csproj`: 30/30 test
  geçti (2 yeni assertion dahil: kabul/red sonrası tam olarak 1 audit satırı var).
- Mutasyon kontrolü: `git stash push -- src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs` ile dosya eski
  (transaction paylaşmayan) haline döndürüldü, yeni atomiklik testi beklenen şekilde kırmızıya döndü
  (`Expected ... OrderItemState.Active, but found OrderItemState.Cancelled`) — denetimin bulgusunu birebir
  doğruluyor. `git stash pop` ile geri getirildi (aynı stash, kayıp yok), yeniden derleme sonrası paket tekrar
  22/22 yeşile döndü.
- `ALKAROS.Orders.csproj` ve `ALKAROS.Host.csproj`: sıfır hata, sıfır uyarı ile derlendi.

## Handoff

- None
