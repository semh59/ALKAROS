# V1-RMD-906 incelemeleri

## Tur 1 (qa-uctan-uca-akis)
- Orta: ikram bill-settlement kilidini almıyordu -> DÜZELTİLDİ (BillMoneyGuard.LockBillAsync sonra ödeme-tahsis kilidi).
- Orta: yolda bekleyen ödemeler sayılmıyordu -> DÜZELTİLDİ (409 COMP_PAYMENT_IN_FLIGHT, test + mutant 5).
- Orta: indirim yolu iadeyi düşmüyor, ikram düşüyor -> kabul, ayrı karar (RESULT.md Açık).
- Orta test eksikleri: kısmi iade ve bekleyen ödeme testleri eklendi; çoklu ödeme ve eşzamanlı tahsilat testleri kabul edilen eksik.
- Düşük: hata mesajında tutar yok (statik sözlük; kabul), para birimi süzülmüyor (tek para birimi TRY).
n## Tur 2 (qa-backend)
- Orta (tur 2): comp ile indirim yolu kilit sırası ters (comp: ayar kilidi -> tahsis kilidi -> hesap satırı; indirim: hesap satırı -> tahsis kilidi) -> DÜZELTİLDİ: comp ayar kilidinden sonra hesap satırını FOR UPDATE alır (ayar < satır < tahsis). Çakışma testi yazılamadı (tek iş parçacıklı test görmez), kabul.
- Düşük: indirim ve ikram iade hesabı iki ayrı SQL; ayrı karar.

## Tur 3 (qa-bulgu-dogrulayici)
- Orta: ikram sipariş satırını settlement kilidinden önce yazıyordu (iptal, temizlik, devir yolları ters sıra) -> DÜZELTİLDİ: uç nokta, siparişi kaydetmeden önce kalemin hesaplarının settlement kilitlerini (id sırasıyla, AcquireSettlementLockAsync) alır; işlem içi kilit sırası: hesap satırı -> tahsis. Çakışma testi yazılamadı (tek iş parçacıklı), kod okumasıyla doğrulandı.

## Tur 4 (qa-uctan-uca-akis): yeni kritik/yüksek yok
- Düşük: hesap listesi kilitten önce okunuyor; aradaki pencerede açılan yeni hesap settlement kilidi olmadan güncellenebilir (hesap satırı, tahsis kilidi ve yoldaki ödeme kontrolü korur). Kabul; devir yolu gibi yeniden okuma ayrı iyileştirme.
