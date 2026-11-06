# V1-RMD-856 sonuç

Kendi onayı kararı tek yerde, sunucuda: iade isteği satırına ve bekleyen kart çekildi onayına `mayDecide` eklendi (isteği yapan çağıran değilse ya da `approvals.manager_self_approval_enabled` açıksa true). Ekranlar yalnız bu alana bakar; kullanıcı kimliği ya da ayar istemciye taşınmadı. Sunucudaki asıl karar (`EnsureMayDecide`, `SAME_ACTOR`) değişmedi.

## Değişenler
- Sunucu: `RefundRequestV1.MayDecide` (PaymentRefunds.cs), iade listesi uç noktası (BillingSplitApplication.cs), `PendingCardConfirmationV1.MayDecide` ve tahsilat özeti (DualScreenApplication.Payments.cs).
- PosTerminal: `RefundPanel.tsx` mayDecide false ise Onayla/Reddet/bütçe alanı yerine "Başka bir yetkili onaylamalı."; `refundApi.ts`, `strings.ts`.
- Kasiyer ekranı: `split-payment.js` iade satırında aynı; kart çekildi onayında artık `mine` yerine `mayDecide` (ayar açıkken kendi bildirimini onaylama izni ekranda da var; önceden ekran sunucudan katıydı).

## Testler (kırmızı -> yeşil)
- Sunucu: `TheListSaysWhetherTheCallerMayDecideEachRequestFromTheSharedSetting` (red-server.log), `ThePendingCardConfirmationSaysWhetherTheCallerMayDecideItFromTheSharedSetting` (red-confirmation.log).
- PosTerminal: RefundPanel.test.tsx "offers a request the server says the caller may not decide only the note..." (red-panel.log).
- Kasiyer: split-payment-refunds.test.js iki yeni test (iade satırı; kart onayı mayDecide true/false), red-cashier.log.

## Mutasyon (cp+cmp ile geri alındı)
S1 liste her zaman true, S2 liste ayarı yok sayar, S3 onay her zaman true, S4 onay ayarı yok sayar: hepsi ÖLDÜ (mut-S*.log). C1 RefundPanel mayDecide denetimi kaldırıldı, C2 açıklama metni kaldırıldı, C3 kasiyer iade satırı denetimi kaldırıldı, C4 kart onayı yeniden `mine`: hepsi ÖLDÜ (mut-C*.log).

## Koşular
- Billing iade sınıfı 46+30 yeşil (green-billing-1/2.log), PaymentTender özet testi yeşil (green-confirmation.log).
- vitest: StaticApps split-payment + dayanıklılık + hata sözlüğü 149/149 (green-static.log); PosTerminal billing 75/75 (green-posterminal.log); a11y/dokunma 56/56 (green-a11y.log).
- tsc --noEmit exit 0; oxlint exit 0 (önceden var olan autoFocus uyarıları bu görevle ilgisiz).

## Sınırlar
- Kasa hareketi ve kasa mutabakatı ekranları zaten süzgeçli (cash-session.js, sunucu `selfApprovalEnabled`); değişmedi.
- Eski sunucu `mayDecide` göndermezse kasiyer ekranı kendi kart bildirimini onaylanabilir gösterir; sunucu ve istemci birlikte dağıtılır.
- `mayDecide` "yetkilisin" demek değildir; yalnız kendi onayı kısıtına takılmadığını söyler. Onay yetkisi (payments.refund.approve, reconciliation.manage) sunucuda ayrıca denetlenir; yetkisiz kullanıcı düğmeyi görürse 403 alır (bu görevden önce de böyleydi).
- Kart bildirimi reddi için kendi kendine kısıt yoktur; alan yalnız onayı temsil eder, ekran reddi "Bildirimi geri çek" olarak bırakır.
- Ayar kayıtlı değilse `IsEnabledAsync` ilk okumada kayıt yazar (mevcut davranış); liste istek başına bir kez, kart özeti yalnız bekleyen onay varken okur.
