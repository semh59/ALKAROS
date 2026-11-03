# V1-RMD-785 - Sonuç

Müşteri ekranı kısmi ödemeyi gösterir; ödeme sayfasında açılan hesabın siparişi ekrana bağlanır.

## Kök nedenler (kodda doğrulandı)

- Kısmi ödeme: ekran verisi yalnız `orders.orders` ve `order_items` tablolarından kuruluyordu; ödeme bilgisi yoktu ve bildirim yalnız hesap kapanınca gidiyordu (`TryCloseBillAsync`).
- `/cashier` siparişi gelmiyor: ekran yalnız `customer_display.terminals.active_order_id` işaretçisindeki siparişi gösterir; ana ekran işaretçiyi bağlar, vanilla kasa (table-draft, submit-draft, send-to-cashier) bağlamaz. Ürün sahibi kararı: ödeme sayfasında hesap açılınca bağlansın.
- Eşleşmede tekrarlayan 409: tasarım gereği. Ekran kodu aldıktan sonra her 2 sn'de `complete` sorar, kasiyer onaylayana kadar sunucu `NotApproved` 409 döner, istemci bunu "bekle" sayar (CustomerDisplay.tsx). Zararsız, kod değişmedi.
- Bir kez 401 snapshot: ilk açılışta ekranın oturum çerezi yoktur; 401 üzerine ekran yeni eşleşme kodu ister. Tasarım gereği, kod değişmedi.

## Değişiklik

- `CustomerDisplaySnapshotDto`: `PaidTotal`, `RemainingTotal`. Hesap `IBillPaymentClosureProjector` sonucundan (indirim ve bahşiş dahil tutar, onaylı ödemelerin dağıtımı; tam iade sayılmaz, kısmi iade sayılır), iptal hesaplar dışlanır (`CustomerDisplayPaymentProgress`).
- Her tahsilattan sonra `SnapshotChanged` (`PaymentChanged`), kapanışta `BillSettled`.
- `POST /api/v1/terminals/{terminalId}/billing/bills/{billId}/show-on-display`: terminalin işaretçisini hesabın siparişine bağlar; Draft aktif sipariş varken bağlamaz (`bound: false`). Ödeme sayfası (`split-payment.js`) hesabı yükleyince çağırır.
- Müşteri ekranı: "KALAN TUTAR", "Hesap toplamı", "Ödenen".

## Doğrulama

Backend (gerçek Postgres, port 55432), kırmızı sonra yeşil:

- Kırmızı: `backend-red/` (16 testten 9 kırmızı), `tender-red/` (1), `api-red/` (1), `vitest-red.log`, `static-red.log`.
- Yeşil (son): `backend-final/` 21/21 (üç yeni sınıf), `tender-final/` PaymentTender tüm proje 62/62 (3 yeni gerçek HTTP testi dahil: eşleştirme, bağlama, kısmi EFT, ekran verisi; yetkisiz 403; bilinmeyen hesap 404); önceki ara koşular: `backend-new3/` 16/16; DualScreen sınıfları: `ds-AccessHardeningTests` 17, `ds-DualScreenStoreTests` 29, `ds-rest` 59; PaymentTender `tender-green` ve `tender-red` içindeki 58 geçen; CashSession 25/25; Billing 113/113; ApiConventions 5/5; ModuleBoundaries 17/17.
- Frontend: PosTerminal vitest 837/837, tsc 0, lint 0 hata (eski uyarılar değişmedi); StaticApps 147/147.
- Mutasyon (geçerli, derlenen, kaynak `cmp` ile geri): kalan tutardan ödenenin düşülmemesi, negatif kalan sınırı, iptal hesap filtresi, Draft koruması (birlikte uygulandı, 6 test kırıldı); yalnız kapanışta bildirim (1 test kırıldı); ekran `partlyPaid` (1 test kırıldı); bağlama çağrısı kaldırma (2 test kırıldı).
- Mevcut `ABillThatIsNotSettledYetLogsNothing` testi eski davranışı (kapanmayan hesapta bildirim yok) sabitliyordu; bilerek değişti: `ABillThatIsNotSettledYetStillTellsTheDisplayWhatChanged`. `split-payment-failures.test.js` "hiç POST yok" iddiası bağlama çağrısını dışlayacak şekilde daraltıldı.
- Atlanan test yok. `DualScreen` Host testi tek seferde çalıştırılınca bellek yüzünden kesildi (62 geçti, 0 hata, yarım); sınıf sınıf yeniden koşuldu.

## Ek mutasyonlar

- Görüntü ucundan `AttachAsync` çağrısını kaldırmak: gerçek HTTP testi kırıldı (`mut-http/`).
- `AttachAsync` içindeki durum denetimini kaldırmak: 2 test kırıldı (`mut-state/`).

## İncelemeler (sırayla)

- qa-backend: terminalde `Preparing` vb. durumda takılı sipariş bağlamayı engelliyordu; koruma yalnız `Draft` oldu (test: Submitted ve Preparing). Ödenmiş hesap artık bağlanmaz. Tamamlanmış/boş durumda hesap okumaları atlanır. Ekranda "Hesap toplamı" = ödenen + kalan (indirim ve bahşiş tutarlı). Kabul edilip değişmeyenler: hesabın şubesi denetlenmez (tek işletme; diğer hesap uçları da aynı), Attach hatası yutulmaz (sessiz catch yasak; okuma hatası zaten tüm anlık görüntüyü düşürürdü).
- qa-frontend: 403/429 yanıtları artık konsola yazılır (test eklendi). Tipin zorunlu kalması bilinçli (sunucu her zaman gönderir). Sayfa yeniden yüklenince çağrının tekrarı zararsız (aynı sonuç).
- qa-test-kalitesi: Completed/Idle ve siparişsiz anlık görüntüde hesap okunmaması, aynı hesabı tekrar bağlama, uç HTTP katmanı (yetki, 404, ekran oturumlu anlık görüntü ve `orders/active`) testleri eklendi. Tamamen iade edilen ödeme kapsamı projector'a aittir (mevcut `BillPaymentClosureProjectorTests`).

## Docker yenileme ve canlı doğrulama

- Yedek: `pg_dump -Fc` (8,7 MB) oturum scratchpad'inde `alkaros-pre785.dump`.
- Derleme: `docker-build.log`, `docker-build-all.log` (exit 0, bugünkü HEAD); yığın `compose.yaml` + `compose.dev.yaml` ile `up -d --no-build --wait` (`up.log`, exit 0), volume'lara dokunulmadı, `down -v` / prune yok.
- Migration: öncesi 208 kayıt / son 209 (`migrations-before.log`), sonrası 209 / son 210 (`migrations-after.log`); yeni migration bu görevden değil (785 şema değiştirmez), HEAD'deki önceki görevlerden; hata yok. Tüm kapsayıcılar sağlıklı.
- Canlı uçtan uca (`live-e2e.log`, `live-e2e.py`): giriş, taslak, onay, kasaya gönder, hesap, ekran eşleştirme (display 8091 / kasa 8090), `show-on-display` -> `bound: true`, ekran anlık görüntüsü hesap açılınca sipariş + `remainingTotal` 260; ₺100 kısmi nakit sonrası `paidTotal` 100, `remainingTotal` 160, mesaj "Ödemenizin bir bölümü alındı; kalan tutar ödenecek.".
- Terminal d60b3df9 Semih'in tarayıcısına bağlı (`TERMINAL_KEY_MISMATCH`); onun oturumunu bozmamak için sıfırlanmadı, betik yeni bir terminal kimliğiyle giriş yaptı (`live-terminal-id.txt`; sunucu bu girişe 2afb47c8-b7b5-4815-8bd4-df586660f92f terminalini verdi).
- Müşteri ekranı tarayıcısı (UI) elle açılıp görsel kontrol yapılmadı; anlık görüntü API düzeyinde, arayüzün okuduğu aynı veriyle doğrulandı, arayüz davranışı vitest ile.

## Test verisi (silinmedi)

- Sipariş 8060b159-7d54-4fef-85e8-1dd2757f328a (KASA-1, Adana Kebap x1, 260 TL, Submitted).
- Hesap e3e478be-6d4e-4e87-932e-6031464e96b1 (BILL-TBL-KASA-1-08123426-8060b159): ₺100 nakit alındı (ödeme 89865c8f-2426-4098-8ac4-26145eb84b02), ₺160 kalan.
- Kasa oturumu f38d043c-3aba-4425-8e07-d0254718e9bb (terminal 2afb47c8, yeni açıldı, açık).
- Müşteri ekranı a26b2f2a-cc4b-4be5-83e2-f34a1cadf096, terminal 2afb47c8'e eşleşti (oturum ~12 saat).
- Stok bakiyesi Adana Kebap için 1 porsiyon düştü.

## Açık risk

- Hesabın şubesi/terminal eşleşmesi denetlenmiyor (tek işletme; diğer hesap uçları da aynı).
- `/cashier` sepeti ödeme sayfası açılana kadar müşteri ekranında görünmez (ürün sahibi kararı).
