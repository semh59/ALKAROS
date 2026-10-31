# V1-RMD-772 sonuç

Bulgu doğrulandı: HEAD'de 9 testten 3'ü kırmızı (red/pytest.log). Kök neden kod bozukluğu değil, test bayatlığı: Garson PWA js/ modüllerine bölünmüş, test yalnız waiter-app.js'e bakıyordu; kimlikler de değişmişti.

Test, güncel gerçek yapıya göre düzeltildi (üretim kodu değişmedi, doğrulama gevşetilmedi):
- Kaynak: waiter-app.js + js/**.
- zoneList -> zoneChips; orderDrawer/orderModal/statusRibbon/btnSendKitchen -> billSheet/optionsSheet/ribbon/btnSendFromMenu/btnSendFromBill.
- Terminal kimliği anahtarı js/util.js içinde (birleşik kaynakta aranır).
- Kategori ucu: kodda ayrı uç yok (waiter-app.js yorumu); test artık /catalog kullanıldığını ve /catalog-management/categories çağrılmadığını doğrular.
- flushOfflineQueue/queueOrderAction/dispatchInFlight/btnSendKitchen.disabled -> flushQueue/queueOrder/state.sendInFlight/iki gönder düğmesinin disabled atamaları (ilk üç düzeltmenin gizlediği ek bayat iddialar).
- Yeşil: green/pytest.log 9/9, exit 0.
- Mutasyon (mutation/mutation.log): 15 mutant (zoneChips, billSheet, btnSendFromBill, terminal anahtarı, kategori ucu, /catalog, sendInFlight, flushQueue, queueOrder, çift gönderim koruması ve sıfırlaması, yalnız başarıda silme, api response.ok) ilgili testi kırdı; her kaynak cmp+git diff ile geri alındı.
- Sınır: kaynak-metin testi; davranış (SignalR, gerçek kuyruk) tarayıcıda çalıştırılmadı.
- İnceleme 1 (qa-frontend): kimlik eşleşmeleri ve kategori iddiası doğru; zayıf sendInFlight/response.ok iddiaları güçlendirildi, bayat yorum düzeltildi.
- İnceleme 2 (qa-test-kalitesi): gevşetme yok; disabled/sendInFlight iddiaları offline-queue.js ile sınırlandı (+2 mutant: 15 toplam). Bilinen düşük: birleşik kaynakta alt dize araması ve girinti-duyarlı iki iddia (davranış testi değil).
