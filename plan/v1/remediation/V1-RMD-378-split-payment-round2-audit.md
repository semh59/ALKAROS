# V1-RMD-378 - Hesap Ödeme (Split Payment) Tur 2 denetimi: onay bekleyen kart tahsilatı sessizce bayatlıyordu

- Task ID: V1-RMD-378
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 3: Hesap Ödeme
(Split Payment, `src/Clients/Cashier/wwwroot/payments/split-payment/**`). Tur 1'de bu beş boyut
"ek bir ürün-katmanı gözlemi bulunmadı" diyerek yüzeysel kapatılmıştı. Tur 2, gerçek bir T7
(rol-arası haberleşme) boşluğu buldu.

İlk incelenen ihtimal (bu ekranın "eşit böl / serbest tutar" akışının PosTerminal'in
`BillSplitWorkspace`'inin "ürüne göre böl" moduna göre eksik olduğu) YANLIŞ çıktı: bu iki ekran
rekabet etmiyor, tamamlıyor — `BillSplitWorkspace` yalnızca "kim ne kadar borçlu" TASARIMINI
kaydediyor (kendi Goal metni: "ödeme işlemi yapılmaz"), bu ekran ise gerçek parayı topluyor. Bu
mimari kasıtlı ve doğru; bir bulgu değil.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/29-split-payment-pending-confirmation-poll.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-378-split-payment-round2-audit.md`

## In scope

1. **[T7, Yüksek — rol-arası haberleşme] Onay bekleyen bir kart tahsilatı, ekran açık kalsa bile
   kendiliğinden hiç yenilenmiyordu.** V1-RMD-283'ün kendi iki-kişi kuralı: bir müdür fiş
   numarasıyla "kart çekildi" bildirir, FARKLI bir müdür onaylar. Ama bildirimi yapan müdürün
   ekranı, ikinci müdür (başka bir terminalde/oturumda) kararını verene kadar "Onay bekliyor"
   metnini SONSUZA kadar gösteriyordu — hiçbir polling, hiçbir canlı bağlantı yoktu, yalnızca
   elle sayfa yenilemek durumu güncelliyordu. Gerçek bir onay bu şekilde uzun süre fark
   edilmeden bekleyebilirdi. Şimdi, yalnızca gerçek bir onay bekleyen tahsilat varken (başka
   zaman gereksiz istek atmadan) 5 saniyede bir `refreshSummary()` çağrılıyor; durum çözülünce
   polling kendiliğinden durur.

## Out of scope

- Daha büyük bir çapraz-rol keşif sorunu — bir müdürün, üzerinde ZATEN bu billId'nin sayfası
  açık olmayan bir onay bekleyen tahsilatın VARLIĞINI nasıl öğreneceği (ör. genel bir "onay
  bekleyenler" listesi, WaiterPwa'nın till-queue'sindeki gibi bir canlı bildirim) — bu sayfanın
  kendi kapsamının ötesinde, yeni bir backend yüzeyi (hub yayını + keşif ekranı) gerektiren ayrı,
  daha büyük bir görev. Bu görev yalnızca ZATEN açık olan bir ekranın bayatlamasını çözdü.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier` tam paketi (68 test, 29 numaralı yeni dosya dahil): tam sonuç aşağıda.
- Yeni test GERÇEK İKİ bağımsız tarayıcı bağlamı (iki farklı müdür oturumu) kullanıyor: birinci
  müdürün sekmesi HİÇ yenilenmeden (`reload()` çağrılmadan), ikinci müdürün TAMAMEN ayrı bir
  bağlamdaki onayının birinci sekmeye 8 saniye içinde yansıdığını kanıtlıyor.
- Mutation-check: `split-payment.js`'teki `ensurePendingConfirmationPoll`/`render()` çağrısı
  `git stash` ile geri alınıp yeni testin GERÇEKTEN kırmızı olduğu (zaman aşımına uğradığı)
  doğrulandı; geri yüklenip tam paket tekrar yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı.

## Handoff

- None
