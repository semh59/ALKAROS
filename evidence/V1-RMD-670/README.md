# V1-RMD-670 — Yetki talebinde gerçek tutar, onaylayan yetkisi, kısıtlı kullanıcı, çevrimdışı tekrar

Kapanan bulgular (V1-RMD-597): B009 (Yüksek), B008, B013, B015.

| Bulgu | Düzeltme | Kanıt testi |
|---|---|---|
| B009 | Gönderilmiş kalem iptal talebi kalemin brüt tutarıyla açılır (eskiden 0). Yüzde indirim talebi, hesabın ödenecek tutarının yüzdesi olarak TL'ye çevrilir; sabit tutar aynen kalır | `APendingVoidRequestCarriesTheItemsRealAmount`, `APercentageDiscountRequestIsRaisedForTheCurrencyItWouldTakeOff`, `AFixedAmountDiscountRequestKeepsItsOwnAmount` |
| B008 | Onaylayan/reddeden, talep edilen izne (iptal, ikram, indirim) kendisi de sahip olmalı; yoksa 403 `APPROVER_LACKS_PERMISSION`, talep bekler | `AnApproverWhoCannotDoTheRequestedThingThemselvesIsRefused` |
| B015 | Davranışsal kısıtlama açıkken çözücüler (kişisel bütçe, yetki devri) atlanır, talep yöneticiye gider | `AnOpenTighteningSendsTheRequestToAManagerEvenWhenAResolverWouldApproveIt`, `WithoutATighteningTheResolverStillApproves` |
| B013 | Çevrimdışı uzlaştırmada aynı anahtar başka kullanıcı/izin/konu/tutar/sebep taşırsa 409 `IDEMPOTENCY_KEY_REUSED`; aynı eylem tekrarı eskisi gibi "already reconciled" | `ResendingAKeyWithAnotherAmountIsAConflictWhileTheSameActionIsAReplay` |

## Koşu (test-*.log)
Identity Authorization 230/230, Host Authorization 10/10, VoidSent 19/19, Billing 71/71, OfflineReconciliation 11/11, Orders.Comp 16/16, Host.Errors 180/180; 0 başarısız. Mevcut iki yetki testinin tohumu, onaylayana üç hibe iznini de verecek biçimde güncellendi (onaylayan yetkisi kuralı gereği); iki devir testi eski elle ekleme için `withGrantPermissions: false` kullanır.

## Mutasyon (mutations.log, mutate.mjs)
9 mutant: 8 öldü, 1 hayatta (D2). D2, yüzdenin "ödenecek tutar" yerine "ara toplam" üzerinden hesaplanması; fiyatlar KDV dahil olduğundan bu iki tutar aynıdır (ara toplam = ödenecek), yani davranışsal olarak eşdeğer mutant; test bunu ayırt edemez ve etmesi gerekmez. Her turda dosya orijinale döndürüldü, cmp ile doğrulandı.

## Bilinen sınırlar
- Eski bekleyen iptal talepleri (tutar 0): aynı anahtarla yeniden gönderilirse 409 verir; talepler geçicidir.
- Devir kaydı (delegation_id), onaylayan≠talep eden CHECK'i, değişmez alan tetikleyicisi ve yeni yabancı anahtarlar veritabanı göçü gerektirir: V1-RMD-673 (B010, B017, B018).
- Devrin kullanım anında devredenin aktifliği ve kümülatif tavan: ürün kararı belirsiz, açık.
- Yeni 403 iletisinin yönetici ekranında gösterilmesi V1-RMD-669.
