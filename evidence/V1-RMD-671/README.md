# V1-RMD-671 kanıtı

- B007: çevrimdışı eylemler geçici hatada (bağlantı, 401/408/423/429, 5xx) cihazda kalır; yalnız kalıcı ret siler ve Türkçe neden gösterilir. Kuyruk 50'lik partilerle gider.
- B011: bütçe başına danışman kilidi; parti içi çift sayım kaldırıldı; yeniden girişte aynı kullanıcının geçerli bütçelerde harcadığı sayı düşülür.
- B012: parti en çok 50 eylem; hepsi yazmadan önce doğrulanır; ileri saatli ya da bütçeden önceki saatli eylem yalnız kendisi reddedilir. Hız sınırı eklenmedi (plan: kapsam dışı).
- B014: hata gövdesi ortak `error` zarfında.
- B023: kullanıcı değişince önceki kullanıcının taslak/çevrimdışı eylem/hatalı sipariş/istek anahtarları silinir; istek anahtarları yenilemede kalır; başarısız çıkışta oturum açık kalır ve uyarı çıkar. Çevrimdışı iptalin tutarı artık kalemin gerçek tutarı (V1-RMD-670 ile uyumlu).
- B030: sipariş kuyruğu 401/403/408/409/423/429'da turu hatalı listesine atmaz.
- Testler: test-offline-module.log (41), test-offline-host.log (12), test-staticapps.log (108) yeşil.
- Mutasyon: mutations.log, 13 mutantın 13'ü yakalandı (V1 ilk denemede derleme hatası, düzeltilip yeniden yakalandı).
- Doğrulanamadı: cihazda/tarayıcıda elle deneme yapılmadı. Başka kullanıcının gönderilmemiş sipariş turu yeni kullanıcının adıyla gidebilir (ürün kararı bekliyor). Çıkışta push aboneliği, çıkış başarısız olsa da önce kapatılır.
