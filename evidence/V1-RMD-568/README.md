# V1-RMD-568 - Tıklamayı izleme kimliğine bağlama

Ne yapıldı
- Tarayıcı: bir düğme tıklaması ya da form gönderimi sonrası 2 saniye içinde yapılan ilk API çağrısının izleme kimliği o tıklama kaydına yazılır (`trace-correlation` izleme kimliğini duyurur, etkileşim kaydı alır). Rota değişikliği kimlik almaz. Kayıt oluştuktan sonra gelen kimlik de kalıcı kuyruğa işlenir.
- Sunucu: etkileşim kaydına isteğe bağlı `traceId`; yalnız 32 küçük harfli onaltılık hane ve tamamı sıfır olmayan değer saklanır, başka her değer kimliksiz saklanır (tıklama reddedilmez). Migrasyon 192 `trace_id` sütunu ve kısmi indeks ekler.
- Kullanıcı yolculuğu: zaman çizelgesi tıklamayı "Düğme" adımı olarak gösterir ve çizelge en erken adımdan (tıklama) başlar; istek adımı artık gerçek başlangıç farkıyla sıralanır. Yalnız tıklama kaydı kalmışsa (istek bellekten düşmüşse) çizelge yine bulunur.

Kabul koşulları
1. Tıklamanın başlattığı istek aynı kimlikle bağlanır: tarayıcı testleri (`attaches the trace id...`, `stores the trace id that arrives after...`), sunucu testi `ClickKeepsAValidTraceId...` ve `TimelineStartsWithTheClickThatStartedTheRequest...` (gerçek Postgres).
2. Bağlanamayan tıklama reddedilmez: `keeps the click without an id...` ve sunucuda 7 kayıttan 7'si kabul, 6'sı kimliksiz.
3. Bu klasör. Mutasyon: mutations.log (15/15 yakalandı).

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- "İlk API çağrısı" kuralı zamana dayanır: tıklamadan sonraki 2 saniye içinde başka bir arka plan çağrısı (yoklama) önce giderse tıklamaya o bağlanır; ayırt etmenin yolu yok, gerçek tarayıcıda ölçülmedi.
- Kimlik yalnız PosTerminal'de bağlanır; Kasa ve Garson istemcileri kimlik göndermiyor (bu görevin kapsamı dışında).
- Gerçek tarayıcıda uçtan uca denenmedi (yalnız jsdom ve gerçek veritabanı testleri).
- Tıklama kaydı kuyrukta beklerken gönderildiyse sonradan gelen kimlik kaybolur (gönderim 10 sn'de bir; pencere 2 sn).
