# V1-RMD-581 - Mutfak yazıcısı: Türkçe karakter tablosu, ibare ve durum okuma

Kaynaklar (2026-10-16/17 okundu): Epson TM-T20 hızlı başvuru (ESC t: 13 = PC857 Turkish), Bixolon SRP-350/352plusII komut kılavuzu (ESC t: 37 = 857), Star ESC/POS modu kılavuzu (ESC GS t: 12 = Codepage 857), Epson durum komutları (DLE EOT 1/2), GİB GMP-3 v5.0 madde 15 (mutfak yazıcısı belgesi ibaresi).

Değişiklikler
1. `EscPosEncoding.cs` (yeni): yazıcı profili (Epson, Bixolon, Star, Ascii) ve Türkçe kod sayfası 857 ile kodlama; fiş eskiden UTF-8 gidiyordu, ç ğ ı ö ş ü bozuk basılırdı. Profil `ALKAROS_PRINTER_PROFILE` ile seçilir, varsayılan Epson. Ascii profilinde Türkçe harfler ASCII karşılığına çevrilir.
2. Yazımdan sonra `DLE EOT 1` ile durum sorulur; çevrimdışıysa `DLE EOT 2` ile neden okunur: kağıt bitti, kapak açık, hata, bilinmeyen. Bu durumda "teslim belirsiz" istisnası atılır (otomatik yeniden deneme yok). Yazıcı yanıt vermezse eski davranış sürer.
3. Mutfak fişinin sonuna "MALİ DEĞERİ YOKTUR- FİŞ YERİNE GEÇMEZ" satırı eklendi.

Kanıt: `test-runs.log` (Kitchen PrintQueue testleri 38/38, gerçek Postgres dahil), `mutations.log` (11/11 mutant öldürüldü; M6, M9 ve M10 satırlarında çıktı xUnit hata satırıyla başlar, yani test kırıldı).

Sınırlar (doğrulanamadı)
- Gerçek yazıcıyla denenmedi (V0-PRN-001 Blocked, cihaz yok). Testler sahte TCP yazıcıyla yapıldı; durum baytlarının bit anlamları Epson belgesinden alındı, Bixolon ve Star için aynı DLE EOT komutlarının geçerli olduğu bu görevde doğrulanmadı.
- Profil tek ayardır (ortam değişkeni); yazıcı başına profil için veritabanı alanı ve yönetim ekranı yapılmadı. Aynı mutfakta farklı marka yazıcı varsa yanlış tablo seçilir.
- Kodlanamayan karakterler `?` basılır. İğneli yazıcılarda (ör. Epson TM-U220) Türkçe tablo yoktur; o cihazlar için Ascii profili gerekir.
- Kağıt bitmesi yazımdan hemen sonra sorulur; yazıcı henüz basarken kağıt biterse bu görülmeyebilir.
