# V1-RMD-877 incelemeleri

## qa-guvenlik-yetki (kritik/yüksek yok)
- Düzeltildi: ikram gerekçesi büyük/küçük harf duyarsız kabul ediliyordu (kasa ekranında ikram olarak tanınmazdı) -> tam eşleşme + test + mutasyon M9; ikram ekranında "indirim" sızıntısı (DISCOUNT_BELOW_COLLECTED) -> ikram metni.
- Rapor edildi (kapsam dışı/ürün kararı): garson /discount ile ikramdan gevşek limit; onaylı iznin süresiz olması ve geri alma sonrası yeniden uygulama; hesap başına toplam ikram sınırı yok; kısmen ödenmiş hesapta kalandan büyük ikram bir otomatik hakkı yakar (409 döner); bekleyen/ret için denetim olayı yok; tutar kuruş duyarlığı.
- Temiz: garson /comp reddi, başka kullanıcı grant'i yeniden oynatma, terminal sahipliği, eşzamanlı uygulama kilidi.

## qa-frontend
- Düzeltildi: başarıdan sonra yenileme hatasının "bağlantı kurulamadı/tekrar deneyin" göstermesi (çift ikram riski) -> ayrı ileti; gönderim sırasında alanlar kilitli; ağ hatasında anahtar korunumu testi; J5 testi güçlendirildi.
- Rapor edildi: tam ikramla kapanışta "Hesap Ödendi" metni; bekleme sırasında otomatik yoklama yok; render sonrası odak kaybı (indirim/bahşişle ortak kusur); fiş/ikram etiketi sunucu işaretine değil gerekçe koduna bağlı.
