# V1-RMD-684 — Ayar düzenleme ekranı, kişi adları, istemci düzeltmeleri

Sonuç: Müdür artık "Ayar geçmişi" ekranından garson masa sınırını (ve mutfak eşiği, QR onay süresi, tüm aç/kapa ayarlarını) değiştirebiliyor; geçmişte kişi adı görünüyor. V1-RMD-598 bulguları R2, R3 ve R12'nin büyük kısmı kapandı. 19/19 mutant öldü (16 istemci + 3 sunucu; bir tanesi testi güçlendirince).

Semih senaryosu şimdi: Ayarlar → Ayar geçmişi → "Garson başına en fazla açık masa" → Düzenle → 3 yaz → gerekçe yaz → Kaydet. Geçmişte "Zeynep Müdür 0 → 3, gerekçe" görünür. 4. masa açılırken Türkçe uyarı V1-RMD-682'de.

Ne değişti:
- **Düzenleme**: yalnız ekranın aralığını bildiği ayarlar (masa sınırı 0-50, mutfak eşiği 1-99, QR süresi dakika olarak, tüm Evet/Hayır ayarları); gerekçe zorunlu; boş, değişmemiş, aralık dışı ve tam sayı olmayan girişler sunucuya gitmez; sunucunun Türkçe reddi gösterilir; başkası araya girmişse (409) düzenleyici kapanır, liste yenilenir ve Türkçe uyarı çıkar.
- **İlk ziyarette listeleme**: ayarlar ilk okunana dek kayıtsızdı (liste boş, PUT 404). Liste ucu artık bilinen tüm ayarları kaydeder.
- **Kişi adı**: geçmiş ucu `changedByName` döndürür (güncel görünen ad); ad bulunamazsa "Bilinmeyen personel", sistem değişikliği "Sistem"; kimlik parçası görünmez.
- **Gösterim**: süre "5 dakika", sınır "Sınırsız / 3 masa", saat İstanbul dilimi, "Setting deactivated" ve "[Deactivated]" Türkçe.
- **Hızlı tıklama yarışı**: `usePanel` yalnız en yeni isteğin cevabını işler (yavaş eski cevap yenisini ezmez).
- **İşletme kimliği ekranı**: kayıt, ekranın yüklediği sürümle gider (eşzamanlı değişiklik yakalanır, 409'da yenilenir); yükleme hatasında "Tekrar dene"; kayıt sırasında oturum biterse giriş formuna dönüş; logo kaldırma onay ister; gerekçe metninde ürün adı yok; kaldır düğmesi 48 px.

Kanıt: `test-host-settings.log` (27/27), `vitest.log` (79 dosya, 715 test), `tsc.log`, `oxlint.log` (uyarılar eskiden kalma, bu görevin dosyalarında yok), `mutations.log`.

Açık kalanlar (R12g'nin küçük kısmı, yeni görev): hata iletisinin kart içinde gösterilmesi, tablo yatay kaydırma kabı, işletme kimliği CSS'inin `--ds-*` değişkenlerine geçişi, ölü idempotency başlığı (Invoicing), logo okuma hatasında null yerine hata. Ayrıca garson seçicide "sınır dolu" rozeti (ürün kararı) ve ayarı yeniden etkinleştirme.
Doğrulanamadı: tarayıcıda elle deneme.
