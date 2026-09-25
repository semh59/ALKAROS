# V13-GOV-006 - Token payment request adapter, unverified draft

Bu klasör, `V13-GOV-006`'nın (bkz. `plan/v1.3/governance/
V13-GOV-006-token-payment-request-unverified-draft-authorization.md`)
kendi Owned surface'ı altında ürettiği tek deliverable'dır: `V13-HUG-001`
(kart ödeme request path) gerçekten başlayabilmeden önce, sadece
dokümana dayalı, doğrulanmamış bir taslak adapter.

## Neden burada, `src/Modules/Payments/Token/**` altında değil

`V13-HUG-001`'in `Dependencies`'inde `V0-HUG-001` var, ve o hâlâ
`Blocked` (gerçek Token client-id/client-secret, test terminali yok).
`plan/TASK_STANDARD.md:57`/`:97` gereği bu bağımlılık `Done` olmadan
`V13-HUG-001`'e `Status: InProgress` bile verilemez. Bu taslak, o kuralı
bypass etmeden ilerleyebilmek için **`ALKAROS.slnx`'e hiç eklenmeyen,
bağımsız derlenip test edilen** bir proje olarak burada tutuluyor.
`V13-HUG-001` gerçekten başladığında, buradaki kod referans alınıp o
görevin kendi Owned surface'ına (`src/Modules/Payments/Token/
PaymentRequest/**`) taşınır/uyarlanır — bu klasördeki kod asla doğrudan
production'a girmez.

## Doğrulanan vs DOĞRULANMAYAN

**Doğrulanan (gerçek Token dokümantasyonuna karşı, 21/21 test yeşil):**

- Auth akışının gerçek tuhaflığı: `POST /v1/auth/token` başarı durumunda
  body `status: 201` döndürüyor (diğer TÜM endpoint'lerin `status: 0`
  konvansiyonunun aksine) — bu asimetriyi ilk yazımda ıskalamıştım, kendi
  testim yakaladı, ayrı bir kod yoluyla düzeltildi.
- `Add Instant Basket` isteğinin header'ı sadece `terminal-id`
  taşıdığını, asla `branch-id` taşımadığını.
- Belgelenmiş hata kodlarının (`1100`, `1104`, vb.) `TokenApiException`
  olarak fırlatıldığını.
- `Get Basket Details`'in tamamlanmış-basket örneğinin (`sale` objesi)
  doğru parse edildiğini.
- Polling'in zaman aşımında `null` döndürdüğünü, ve `TokenTenderHandler`'ın
  bunu ASLA Approved/Declined'a değil, `RequiresReconciliation`'a
  eşlediğini (CORR:C29 invariant'ı — timeout hiçbir zaman zımni onay/ret
  değildir).
- `sale.status` 99 (fiş iptali/void) gibi bu görevin kapsamı dışındaki
  bir durumun da Approved'a düşmediğini.
- Belgelenmiş `1100`/`1104` hata kodlarının, ve auth'un HTTP 401 ile
  bozuk/HTML cevaplarının doğru `TokenApiException` fırlattığını.
- `AddInstantBasketAsync`'in terminal isteği REDDETTİĞİNDE (örn. 1100)
  bunu ASLA sahte bir `Declined` sonucuna çevirmediğini — exception olarak
  yukarı fırlattığını (kart hiç sunulmamışken "kart reddedildi" yalanını
  söylememesi için).
- Yemek kartı routing şemasının (`type: 7`, `operatorId: 1005` TokenFlex)
  doğru serialize edildiğini — bu SADECE `TokenBasketClient`'ın genel
  kapasitesini kanıtlıyor, `TokenTenderHandler`'ın (BankCard-only,
  `type: 3`, `operatorId: 0` = zorlanmamış banka seçimi) meal card
  gönderdiği anlamına gelmiyor; o `V13-MCD-004`'ün kapsamı.
  `V13-GOV-006`'nın kapsam dışı listesi bunu değiştirmiyor.
- Token cache'inin 86400s'lik pencereyi doğru şekilde dolduğunu (enjekte
  edilebilir bir clock ile, gerçek `Task.Delay` olmadan test edildi).
- `CancellationToken` iptalinin yutulmadan yukarı fırlatıldığını.
- Polling'in sonucun TAM son denemede geldiği sınır durumunu doğru
  yakaladığını.

Bu genişletme sırasında yakalanan **ikinci gerçek bug**: ilk testlerde
`handler.Requests[i].Content!.ReadAsStringAsync()` ile gönderilen body'yi
doğrulamaya çalışan 2 yeni test `ObjectDisposedException` ile patladı —
çünkü `TokenBasketClient`'ın kendi `using var request = ...` bloğu,
metod dönene kadar `HttpRequestMessage`'ı (ve içeriğini) dispose ediyor.
`FakeHttpMessageHandler` artık body'yi `SendAsync` içinde, dispose
edilmeden ÖNCE okuyup ayrı bir `RequestBodies` listesine kaydediyor.

**DOĞRULANMAYAN (gerçek cihaz/sandbox olmadan doğrulanamaz):**

- Bu şemanın TokenX Connect **Cloud**'da (buradaki testler Wire/genel
  dokümantasyondan alınan `paymentItems`/`sale` şeklini kullanıyor,
  V0-HUG-001'in kendi notunda bu bir varsayım olarak işaretli) birebir
  aynı çalıştığı.
- Gerçek bir kredi kartının gerçek onay/ret sürecinin bu akışla eşleştiği.
- `amount`/`price` alanlarındaki ondalık kaydırmanın (kuruş mu, farklı
  bir birim mi) gerçek cihazda doğru yorumlandığı.
- Refund/cancel senaryosu — bu taslak hiç kapsamıyor (`V13-HUG-003`'ün
  işi, ve o konuda da API belirsizliği var, bkz. `evidence/v0/
  integrations/V0-HUG-001/2026-09-18-operator-id-and-void-schema.md`).

## Nasıl çalıştırılır

```text
cd evidence/V13-GOV-006/token-adapter-draft
dotnet test tests/TokenAdapterDraft.Tests.csproj
```

(Windows'ta dotnet PATH'te değilse: `$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"`)

Bu repo'nun kök `Directory.Build.props`/`Directory.Packages.props`'u
(Merkezi Paket Yönetimi + kilitli restore) miras alınır ama bu iki
csproj dosyası bilerek `ManagePackageVersionsCentrally`/
`RestorePackagesWithLockFile`/`RestoreLockedMode` = `false` ile bundan
çıkıyor (kendi bağımsız sürüm/lock yönetimi).

## `V13-HUG-001` gerçekten başladığında yapılacaklar

1. `src/Modules/Payments/Token/PaymentRequest/` altında gerçek projeyi
   oluştur (bu projeyi `ALKAROS.Payments.csproj`'a dahil et, ayrı bir
   standalone proje olarak değil).
2. `TokenTenderHandler`'ı gerçek `ALKAROS.Payments.TenderRouting.
   ITenderHandler`'ı implemente edecek şekilde uyarla (şu an sadece
   şeklini taklit ediyor, gerçek arayüze bağlı değil).
3. `TokenBasketClient`'ın auth/instant-basket/poll akışını GERÇEK bir
   test terminaline karşı çalıştır; her varsayımı (kuruş/decimal birimi,
   `sale.status` anlamı, `checkNumber` sınırı) doğrula veya düzelt.
4. `V13-HUG-001`'in kendi Acceptance evidence'ını (gerçek sandbox/cihaz
   transkripti, bir onaylanmış + bir reddedilmiş istek) üret.
