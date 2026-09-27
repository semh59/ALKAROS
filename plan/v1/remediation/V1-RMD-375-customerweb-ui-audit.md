# V1-RMD-375 - CustomerWeb (Menu/OrderEntry/Bill) modül denetimi: sessiz canlı güncelleme

- Task ID: V1-RMD-375
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) son üç modülü —
15 (Menu), 16 (OrderEntry), 17 (Bill) — tek görevde birlikte kapatıldı: üçü de
`src/Apps/CustomerWeb/**` altındaki, aynı oturum/sepet sözleşmesini paylaşan kardeş vanilla
JS/HTML/CSS sayfaları (`Menu` 576 satır, `OrderEntry` ~360 satır, `Bill` ~250 satır). On iki
boyut üzerinden tarandı.

Bu istemcinin test altyapısı Cashier/WaiterPwa'dan farklı: gerçek tarayıcı çalıştıran bir
Playwright E2E paketi YOK, yalnızca `tests/Apps/CustomerWeb/**` altında statik dosya/regex
doğrulaması yapan pytest testleri var (`tests/Clients/Cashier/Frontend`'in kendi konvansiyonunu
taklit ediyor). Bu, gerçek bir test-altyapısı boşluğu ama yeni bir Playwright E2E kurulumu
inşa etmek bu görevin ölçeğinin çok üzerinde bir altyapı projesi olurdu — mevcut statik-test
konvansiyonuna sadık kalınarak bulgular buna göre doğrulandı.

Üç sayfa da genel olarak olgun: hiçbiri kendi özel hata sınıfını fırlatmıyor; CSS/JS sınıf adı
uyumsuzluğu taraması sıfır eksik buldu; `businessBrand` (V1-SET-009), sepet/oturum sözleşmesi,
mükerrer-gönderim koruması (`submissionId`, sessionStorage'da kalıcı) hepsi sağlam. Menü sayfası
tek seferlik bir yükleme yaptığı için (canlı güncelleme yok) bulgu YOK — Modül 15 bu görevde
"gerçek bulgu yok" olarak kapandı.

Gerçek, sistemik bulgu — OrderEntry ve Bill'in İKİSİNDE de: her iki sayfa da kullanıcı hiçbir
şey yapmadan kendiliğinden güncellenen içerik gösteriyor (OrderEntry: sipariş durumu polling'i;
Bill: kendi başlığının "Bu ekran canlı güncellenir" dediği polling), ama ikisinde de bu
güncellemeleri duyuran hiçbir ARIA canlı bölgesi YOKTU. Ekran okuyucu kullanan bir misafir,
siparişinin kabul/red edildiğini ya da adisyonuna yeni bir kalem eklendiğini hiçbir zaman
öğrenemiyordu — bu oturumdaki diğer tüm istemcilerin (Cashier/WaiterPwa/PosTerminal) zaten
tutarlı biçimde uyguladığı temel bir kalıbın bu iki sayfada hiç uygulanmamış olması.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Apps/CustomerWeb/OrderEntry/test_customer_web_order_entry.py
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Apps/CustomerWeb/Bill/test_customer_web_bill.py
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-375-customerweb-ui-audit.md`

## In scope

1. **[T1, Yüksek — erişilebilirlik] OrderEntry'nin `#orderStatus` bölümü (sipariş durumu:
   gönderildi → onaylandı/reddedildi, `pollUntilMaterialized()` tarafından sayfa yenilemesi
   olmadan güncelleniyor) hiçbir ARIA canlı bölgesi taşımıyordu.** `role="status"
   aria-live="polite"` eklendi.
2. **[T1, Yüksek — erişilebilirlik] Bill'in `#billList`/`#billSummary`'si — sayfanın kendi
   başlığının "Bu ekran canlı güncellenir" dediği içerik — hiçbir ARIA canlı bölgesi
   taşımıyordu.** `#billList`'e `aria-live="polite" aria-atomic="false"` (Cashier.tsx'in kendi
   `ticket-lines` deseniyle aynı — her pollda tüm listenin yeniden okunmasını önler),
   `#billSummary`'ye `role="status" aria-live="polite"` eklendi.

## Out of scope

- Menu sayfası (Modül 15) tek seferlik, statik bir yükleme yapıyor — polling/canlı güncelleme
  yok, bu yüzden aynı sınıftan bir bulgu buraya uygulanamaz. Kategori sekmesi geçişleri
  kullanıcı eylemiyle tetiklendiği için (odak zaten düğmede), ek bir canlı bölge gerekmedi.
  Modül 15 bu görevde "gerçek bulgu yok" olarak kapatıldı.
- Gerçek bir Playwright E2E paketinin bu istemci için hiç var olmaması ayrı, daha büyük bir
  altyapı boşluğu — bu görevin kapsamı dışında bırakıldı, Semih'in kararına bağlı.
- Ürün-katmanı (P1-P4) gözlemi yok.

## Dependencies

- None

## Acceptance evidence

- `python -m pytest tests/Apps/CustomerWeb` tam paketi (18 test, bu görevin 2 yeni testi dahil):
  18/18 geçti, regresyon yok.
- Mutation-check: `order-entry.html` + `bill.html` `git stash` ile geri alındı, yeni iki test de
  GERÇEKTEN kırmızı oldu. `git stash pop` ile geri yüklendi, paket tekrar 18/18 yeşile döndü.

## Handoff

- None
