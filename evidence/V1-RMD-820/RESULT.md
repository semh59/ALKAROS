# V1-RMD-820 sonuç

Kapatılan bulgular: satır 16 (kasa oturumu sayfası `/active` hata yolları, tahsilat sayfasında kasa durumu okuma hatası ≠ kasa kapalı), satır 25 / T-05 (React kabuğunda Kasa düğmesi ve bandı), K2 (a, b), K4, T-04 (EFT'ye sessiz kayma, pasif çip kontrastı, "Kasayı aç" yolu), K13 (kimliksiz alanlarda odak, eski yanıt ezmesi), F0405 (yoklama çakışması), F0449 (sessiz yenileme hatası).

Zaten kapalı bulunanlar (güncel kodda doğrulandı, tekrar yapılmadı): K12/T-01 (hata ekranı cs-* sınıflarıyla stilli: cash-session.js renderLoadError), K7 (taslaklar captureDrafts ile korunuyor), K8 (kapanışta "kapanmış olabilir" bildirimi ve beklenen tutar yokken pasif düğme), K6 kasa sayfası maxlength/ERROR_TEXT. cs-tab ve role=tab kasa oturumu sayfasında artık yok.

## Doğrulama
- StaticApps: 270/271 (static-final.log). Kalan tek kırmızı `error-dictionary-lock`: G5 çalışma ağacındaki `REFUND_QUANTITY_NOT_WHOLE` kodunun sunucu metni İngilizce ("This product is refunded in whole units only."); düzeltme G5'in sunucu dosyasında (PaymentRefunds.cs). `STOCK_RESTORE_FAILED` cümlesi bu görevde sözlüğe eklendi. waiter-app zaman aşımı yük altında aralıklı (G5 WaiterPwa alanı), tek koşuda geçer.
- PosTerminal vitest 937/937 (vitest-final.log), tsc 0, oxlint 0 hata.
- Playwright E2E (gerçek Host + Postgres 55432): 07, 08, 09, 10, 11, 12, 14, 15, 16, 18, 21, 25, 29, 30, 31 yeşil. Yardımcı `openSplitPayment` kasa kapalıyken EFT'yi açıkça seçer; 25 "hiçbiri seçili değil" ve bandı doğrular; 12'de ipucu metni "Ödeme yöntemi" aramasını bozmasın diye "Devam etmek için bir yöntem seçin." oldu.
- Mutasyon: 13 mutant (mutation-summary.txt), 13 öldürüldü, her dosya cmp ile geri alındı.
- Kırmızı: cash-session-load-errors (5) ve split-payment-cash-state (8) düzeltmeden önce kırmızı görüldü; refresh-guard ve bileşen testleri düzeltmeyle birlikte yazıldı, kırmızı kanıtı mutantlarla.
- Görsel doğrulama bu görevde jsdom/E2E ile; 360-768-1280 ölçümü G8 görevlerinde.

## İnceleme
- qa-frontend: 403 "kasa izni yok" ayrı durum (kalıcı yanlış uyarı), submitTender içinde bayat koruması, bayat uyarı role=status, çift okuma (focus+visibilitychange) giderildi. Ürün notu: kasa kapalıyken yöntem seçilmemiş kalır (PO kararı: sessiz EFT yok).
- qa-ux-tasarim: yeniden okuma etiketi tek ad ("Kasa durumunu yeniden oku"), React tarafında 403 ayrı durum (bağlantı değil düz metin), "Kasa: Kontrol…" kısaltıldı; kontrast hesapları >= 4,5:1.
- Doğrulama turu: 6 madde kapandı; çıkan iki düşük kusur (okuma sürerken manuel yeniden okuma düşüyor, izinsiz durumda bağlantı) giderildi ve testlendi.

## Notlar
- Düğme yalnız `cash.drawer` izni olan oturumda görünür ve okunur (sunucu `/active` ucu aynı izni ister).
- 401 paylaşılan oturum sonlandırma işleyicisine gider; 429/5xx/ağ hatası "Bilinmiyor", 403 "İzin yok" olur, asla "Kapalı" denmez.
