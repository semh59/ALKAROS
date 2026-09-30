# V1-RMD-458 - kabul kanıtı

- Sorun: `stale.test.ts` içindeki "gives the pairing dialog an accessible modal contract" testi tembel yüklenen ekranı sabit 400 ms
  bekliyordu. Kod dönüştürme önbelleği soğukken (test tek başına çalışınca hep) ekran gelmeden "expected undefined to be defined"
  ile kırmızı oluyordu; tam takımda da makine yüküne göre arada kırmızı çıkıyordu (bu oturumda arka arkaya üç kez dahil).
- Düzeltme: sabit tur sayısına ek olarak, beklenen düğme görünene kadar (en çok 5 saniye) bekleyen `waitUntil` yardımcısı. Testin
  doğruladığı davranış değişmedi; yalnız bekleme stratejisi.
- Kanıt: test tek başına üç kez exit code 0 (`tek-basina.log`; düzeltme öncesi aynı komut hep exit code 1 idi), tam takım art arda
  beş kez exit code 0, 394 test (`tam-takim.log`); `typecheck.log` ve `lint.log` exit code 0.
