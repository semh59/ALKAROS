# V1-RMD-1117 - Kasiyerin kendi oturumunda kapanan ödenmiş hesapları listeleyen salt okunur uç

- Task ID: V1-RMD-1117
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Kasiyer deneyimi turu 14: müşteri hesabı ödedikten bir dakika sonra dönüp iade isterse kasiyer kapanmış (ödenmiş) hesabı bulamıyor; sunucuda ödenmiş hesapları listeleyen uç yok (Bekleyen hesaplar ödenmişleri bilerek dışarıda bırakır). Salt okunur `GET /api/v1/terminals/{terminalId}/billing/recent-sales` ucu eklenir: çağıran kasiyerin kendi açık kasa oturumunda (Open, Counting veya Closing) bu kasada kapanan en yeni 20 ödenmiş hesabı döner. Her satır hesap kimliği, masa numarası (yoksa null, ekran Hızlı satış yazar), kapanış zamanı, net tahsil tutarı (tahsisler eksi iadeler) ve ödeme yöntemleri (Cash, Card, Eft, Account; mevcut PaymentMethodLookup tanımı) taşır. Yanıt `hasOpenSession` işareti taşır; oturum açık değilse liste boştur. Bu kasaya ait olmayan hesap (başka kasanın sahipliği) ve oturum açılmadan önce kapanan hesap listelenmez. Yetki `payments.refund.request` (iade isteyebilen, iade edilebilir olanı görür; `refund-requests` listesiyle aynı); yetkisiz çağrı 403 alır. İade, onay, limit ve yetki kuralları değişmez; şema ve migration yoktur.

## Owned surface

- `evidence/V1-RMD-1117/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillingSplitApplication.cs
  - src/Host/Experience/Billing/RecentSalesStore.cs
  - tests/Host/Experience/Billing/RecentSalesHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. Billing HTTP testleri ve ApiConventions mimari testi

## Out of scope

- İade, onay, limit ve yetki kuralları; şema ve migration; istemci ekranı (V1-RMD-1118); önceki oturumların satışları

## Dependencies

- V1-RMD-1116

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- V1-RMD-1118
