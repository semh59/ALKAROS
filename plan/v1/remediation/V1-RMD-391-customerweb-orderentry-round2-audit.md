# V1-RMD-391 - CustomerWeb OrderEntry Tur 2 denetimi: durum takibi 60 saniye sonra donuyordu

- Task ID: V1-RMD-391
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 16: CustomerWeb'in
QR sepet/sipariş ekranı (`order-entry.js`). Sayfa zaten çok olgun: idempotent gönderim
(`submissionId`), oturum yeniden deneme, yarıda kalan bir gönderimi yeniden yükleme sonrası
izlemeye devam etme, canlı duyurulan durum bölgesi (V1-RMD-375).

Ama `pollUntilMaterialized()`'in kendi yorumu bile "outbox delivery is just slow, not failed"
diyordu — buna rağmen `MAX_POLL_ATTEMPTS` (30 × 2 sn = 60 sn) tükendiğinde döngü TAMAMEN
duruyordu ve `#orderStatusMessage` sonsuza dek "Siparişiniz alındı, mutfağa iletilmesi biraz uzun
sürüyor." metninde donuyordu — sipariş sunucu tarafında gerçekten PendingConfirmation/Accepted'a
ilerlese bile müşteri ekranı bunu asla göstermiyordu, sayfayı manuel yenilemek dışında hiçbir yol
kalmıyordu ve bu yol da hiçbir yerde söylenmiyordu. Gerçek bir restoran ağı/mutfak yoğunluğunda 60
saniyenin aşılması "olağan dışı" değil, tam olarak yorumun kendi kabul ettiği senaryo (P2, saha
gerçekliği).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Apps/CustomerWeb/OrderEntry/test_customer_web_order_entry.py
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-391-customerweb-orderentry-round2-audit.md`

## In scope

1. **[P2, Orta-Yüksek — saha gerçekliği] Poll döngüsü 60 saniye sonra kalıcı olarak duruyordu.**
   Hızlı faz (30 × 2 sn) tükendiğinde artık pes etmek yerine daha yavaş bir kadansla (10 sn
   aralıkla, 120 deneme — yaklaşık 20 dakika daha) kontrol etmeye devam ediyor; bu süre boyunca da
   bir terminal duruma ulaşırsa mesaj hemen güncelleniyor. Bu ikinci pencere de tükenirse artık
   sessizce eski mesajda kalmak yerine somut bir yönlendirme veriyor: "Siparişinizin işlenmesi
   beklenenden uzun sürüyor. Sayfayı yenileyin veya garsonu çağırın."

## Out of scope

- Yok.

## Dependencies

- None

## Acceptance evidence

- `python -m pytest tests/Apps/CustomerWeb/OrderEntry/test_customer_web_order_entry.py -q`:
  7/7 geçti (bu görevin yeni testi dahil) — bu dosyanın kendi test yakınsaması, diğer
  CustomerWeb sayfalarıyla aynı statik-metin doğrulama biçimini kullanıyor (gerçek bir JS test
  koşucusu bu vanilla uygulamalar için hiç kurulu değil, bkz. V1-RMD-375'in aynı dosyadaki
  emsal testi).
- Mutation-check: yalnızca `order-entry.js` `git stash` ile geri alındı — yeni test GERÇEKTEN
  kırmızı oldu (`AssertionError: assert 'SLOW_POLL_INTERVAL_MS' in ...`). `git stash pop` ile
  geri yüklendi, dosyanın tam paketi tekrar 7/7 yeşile döndü.

## Handoff

- None
