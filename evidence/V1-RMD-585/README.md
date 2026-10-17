# V1-RMD-585 kanıtı

Bekçi konteyneri (`watchdog` servisi, `ALKAROS.Host.dll watchdog`) ve Telegram doğrudan göndericisi eklendi.

- Testler: 19/19 geçti (`test-runs.log`). Mutasyon: 15 mutant, 15'i öldü, sağ kalan yok (`mutations.log`, betik `mutate.py`).
- Gerçek Docker yığını (`docker-proof.log`): API durdurulunca bekçi yaklaşık 13 sn içinde (2 ardışık başarısız yoklama) tek `system.unreachable` satırını tampon dosyasına yazdı; API dönünce ikinci alarm gelmedi, bekçi çalışır kaldı.
- Telegram bu yığında yapılandırılmadı (`telegramConfigured=False`), bu yüzden gerçek mesaj teslimi **doğrulanamadı** (V1-RMD-557'nin dış hesap konusu). Tampondan boşaltma yolu yalnız birim testle (sahte kanal) doğrulandı.
- Yoklama `X-Forwarded-Proto: https` başlığı taşır; API düz HTTP'yi loopback dışı çağırandan bu başlık olmadan reddeder. Yığında yoklamanın başarılı olduğu (yanlış alarm yok) gözlendi.
- Bot anahtarı hiçbir dosyada yok (`token-scan.log`); yalnız `ALKAROS_TELEGRAM_BOT_KEY` / `ALKAROS_TELEGRAM_CHAT_ID` ortam değişkenleriyle çalışma anında verilir.
