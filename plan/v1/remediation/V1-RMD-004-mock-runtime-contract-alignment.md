# V1-RMD-004 - Mock runtime contract alignment

- Task ID: V1-RMD-004
- Status: Done
- Assignee: Codex-/root
- Work type: implementation
- Surface state: Existing

## Goal

PO:2026-08-24 kararıyla WebPrototype'ı gerçek backend veya production deployment iddiası olmadan; backend model
şekillerini izleyen, başarı ve hata yolları çalışan, yeniden bağlanma ve idempotency davranışı test edilebilir tutarlı
bir mock runtime'a dönüştürmek.

## Owned surface

- `src/Clients/WebPrototype/**`
- `evidence/V1-RMD-004/**`

## In scope

- Table, product, order, queued operation ve session mock verilerini gerçek istemci modellerindeki GUID, row-version,
  idempotency ve operation envelope alanlarıyla hizalamak.
- Cashier ve waiter siparişlerini tek bir asenkron mock service üzerinden submit etmek; aynı idempotency key için
  duplicate oluşturmamak, conflict ve generic failure durumunda taslağı korumak.
- IndexedDB offline kuyruğunu versioned operation envelope ile persist etmek; yeniden bağlanmada FIFO replay,
  mock server acknowledgment ve ilk hatada durma davranışını uygulamak.
- Parçalı ödeme tutarını mock order line verisinden türetmek; mock payment ve print sonucunu açık service
  sonucu olarak sunmak ve hata yolunda başarı mesajı göstermemek.
- Demo session/PIN akışına sınırlı deneme ve cooldown eklemek; arayüzün mock niteliğini açıkça belirtmek.
- Mock service, queue replay, idempotency, conflict, payment derivation ve session lockout davranışlarını gerçekten
  çalıştıran Node testleri eklemek.

## Out of scope

- Gerçek HTTP API, veritabanı, kimlik sağlayıcısı, payment/fiscal/printer cihazı, production security sertleştirmesi
  ve deployment.
- WebPrototype dışındaki C# client, host, solution/project, package, lockfile veya global configuration yüzeyleri.

## Dependencies

- V1-RMD-003

## Acceptance evidence

- `dotnet build ALKAROS.slnx --no-restore`, JavaScript syntax kontrolü ve WebPrototype Node testleri exit code `0` verir.
- Aynı idempotency key ile iki submit tek mock order üretir; table version conflict ve generic failure taslağı
  temizlemez ve başarı mesajı göstermez.
- Offline enqueue browser restart sonrası korunur; reconnect FIFO replay yapar, yalnız acknowledgment alan operation'ı
  siler ve ilk reddedilen operation'da kalan kuyruğu korur.
- Mock payment tutarı seçilen line toplamından gelir; mock print/payment failure durumları UI'da hata olarak sunulur.
- Üç hatalı demo PIN denemesi cooldown başlatır; doğru PIN cooldown süresinde kilidi açmaz.
- Semih; mock cashier submit, waiter offline enqueue/reconnect, conflict, payment failure, printer failure ve PIN lockout
  senaryolarını arayüzde elle doğrulayabilir.
