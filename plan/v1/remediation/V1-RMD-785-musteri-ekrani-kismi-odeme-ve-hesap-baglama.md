# V1-RMD-785 - Müşteri ekranı: kısmi ödeme kalan tutarı ve ödeme sayfasında açılan hesabın ekrana bağlanması

- Task ID: V1-RMD-785
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: implementation
- Surface state: Existing

## Goal

Canlı Docker kasa incelemesinde (V1-RMD-784 raporu) müşteri ekranında iki kusur görüldü ve koddan doğrulandı:

1. Kısmi ödemeyi yansıtmıyor. Ekran verisi (`GetSnapshotAsync`) yalnız `orders.orders` ve `order_items` tablolarından kurulur; ödeme bilgisi modelde yok. Kısmi tahsilatta ekrana bildirim de gitmez: `TryCloseBillAsync` yalnız hesap kapanınca `SnapshotChanged` yollar. Kasada ₺260 hesaptan ₺100 nakit alınınca ekran "ÖDENECEK TOPLAM ₺260,00" göstermeye devam eder. Düzeltme: ekran verisi `PaidTotal` ve `RemainingTotal` taşır; kalan tutar mevcut `IBillPaymentClosureProjector` sonucundan (indirim ve bahşiş dahil hesap tutarı − onaylı ödemelerin dağıtımı) hesaplanır; tamamen iade edilen ödeme sayılmaz, kısmen iade edilen sayılır, iptal edilmiş hesap hiç hesaba katılmaz. Her tahsilattan sonra ekrana `SnapshotChanged` gider.
2. Vanilla kasadan (`/cashier`) girilen sipariş ekrana gelmiyor. Ekran yalnız terminalin `active_order_id` işaretçisindeki siparişi gösterir; ana ekran (PosTerminal) sipariş açınca işaretçiyi bağlar, vanilla kasa sepeti kendi tarayıcısında tutar ve siparişi KASA-1 ortak masasına taslak olarak yazıp kasaya devreder, işaretçiyi hiç bağlamaz. Ürün sahibi kararı (2026-11-03): kasiyer ödeme sayfasında hesabı açınca o hesabın siparişi müşteri ekranına bağlanır (sepet canlı görünmez). Düzeltme: ödeme sayfası hesabı yükleyince `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/show-on-display` çağrılır; sunucu terminalin işaretçisini hesabın siparişine bağlar ve ekrana bildirim yollar. Terminalde düzenlenen (Draft) bir aktif sipariş varken bağlanmaz (ana ekranın açık siparişi ezilmez), `bound: false` döner.

Doğrulanan ve düzeltilmeyen: müşteri ekranı eşleşirken `pairing-requests/{id}/complete` 409 verir çünkü ekran kodu aldıktan sonra her 2 sn'de "tamamla" sorar ve sunucu kasiyer onaylayana kadar tasarım gereği "NotApproved" 409 döner; istemci 409'u "bekle" sayar. Snapshot'ın bir kez 401 vermesi, ilk açılışta ekranın henüz oturum çerezi olmadan snapshot istemesidir; istemci 401'de yeni eşleşme kodu ister. İkisi de tasarım gereği, kod değişmez, `RESULT.md`'de belgelenir.

## Owned surface

- `src/Host/DualScreen/CustomerDisplayPaymentProgress.cs`
- `tests/Host/MigrationComposition/DualScreen/CustomerDisplayPaymentProgressTests.cs`
- `tests/Host/MigrationComposition/DualScreen/CustomerDisplayBillBindingTests.cs`
- `evidence/V1-RMD-785/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenStore.Display.cs (V1-RMD-097) — hesabın siparişini terminale bağlayan sorgu.
  - src/Host/DualScreen/DualScreenContracts.cs (V1-RMD-148) — ekran verisine ödenen ve kalan tutar.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V13-PUI-001) — her tahsilatta bildirim, hesabı ekrana bağlama adresi.
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (V1-IAM-024) — iki ekran verisi adresi ödeme bilgisini ekler.
  - tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs (V1-RMD-008) — sözleşme izin listesi.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx (V1-RMD-097) — kalan tutar gösterimi.
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx (V1-RMD-374) — gösterim testleri.
  - src/Clients/PosTerminal/src/contracts.ts (V1-RMD-031) — ekran verisi tipi.
  - src/Clients/PosTerminal/src/stale.test.ts (V1-RMD-020) — tip alanları için örnek veri.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V13-PUI-001) — hesap yüklenince ekrana bağlama çağrısı.
  - tests/Clients/StaticApps/ (V1-RMD-109) — ödeme sayfası bağlama testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Ekran verisi `PaidTotal` ve `RemainingTotal` taşır; hesap yoksa `RemainingTotal` boştur ve ekran sipariş toplamını gösterir.
2. Kısmi ödeme alınmışsa ekran "KALAN TUTAR" ile kalan tutarı, ödenen tutarı ve sipariş durumu metnini ("Ödemenizin bir bölümü alındı; kalan tutar ödenecek.") gösterir.
3. Nakit, genel ödeme ve kart onayı yollarında her tahsilattan sonra `SnapshotChanged` gider (hesap kapanmasa da).
4. Ödeme sayfası hesabı açınca hesabın siparişi ekrana bağlanır; Draft aktif sipariş varken bağlanmaz.
5. Her düzeltme için önce kırmızı test, sonra yeşil.

## Out of scope

- Vanilla kasa sepetinin canlı görünmesi (sepet tarayıcıda tutulur, sunucu siparişi yoktur), şema/migration, ana ekran akışı, eşleştirme akışı.
- Kart ödemesi gerçek terminal entegrasyonu.

## Dependencies

- V1-RMD-784

## Acceptance evidence

1. Kırmızı sonra yeşil: Host testleri gerçek Postgres'te (ödeme ilerlemesi, bağlama, sözleşme), vitest (PosTerminal ve tests/Clients/StaticApps).
2. PosTerminal lint 0 hata, tsc temiz; ApiConventions ve ModuleBoundaries testleri yeşil.
3. Mutasyon: düzeltmeyi bozan geçerli mutant testleri kırar; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-frontend (veya qa-backend) ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.
6. Docker imajları yeniden derlenir, yığın veri silinmeden yeniden oluşturulur, canlıda taslak→kasaya→hesap→kısmi nakit→müşteri ekranı kalan tutar doğrulanır (`evidence/V1-RMD-785/RESULT.md`).

## Handoff

- None
