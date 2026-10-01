# V1-RMD-478 - Satıcı bilgisi kaydetme ucu idempotency anahtarı alır

CI'daki kırmızı: `MutatingEndpointIdempotencyTests` `PUT .../invoice-settings/seller-profile` ucunu anahtarsız buldu (`V1-RMD-471` ile geldi; o görevde mimari testleri yerelde çalıştırılmamıştı).

## Değişiklik

- `InvoiceSettingsEndpoints.cs`: PUT, `X-Idempotency-Key` başlığını kabul eder (kayıt zaten tekrar edilebilir bir değiştirme; başlık API sözleşmesini karşılar). İzin listesine eklenmedi.
- `sellerProfileApi.ts`: her kaydetmede yeni bir anahtar gönderilir; kart testi anahtarın gittiğini doğrular.

## Kanıt

- `green-architecture.log`: ApiConventions 5/5 (önce 4/5, kırmızı: `mutation-architecture.log` başlıksız hâli gösterir). `green-host.log`: QnbCredentialSettings 17/17.
- `vitest.log`, `typecheck.log`, `lint.log` exit 0; `mutation-client.log`: istemci başlığı gönderilmezse kart testi kırmızı, geri alındı, `cmp` özdeş.

## Çıkarılan ders

- Yeni uç nokta ekleyen görevlerde `tests/Architecture/**` de yerelde çalıştırılır. Ayrıca özellik commit mesajı görev numarasını taşır (CI görev kapsamını mesajlardaki numaralardan okur).
