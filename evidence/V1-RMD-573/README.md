# V1-RMD-573 - Dockerfile apk sürüm sabitleri

Bulgu: `api`/`migrate` ve `connector` imajları derlenmiyordu; Alpine deposunda `libcrypto3`, `libssl3`, `openssl` için 3.5.8-r0 artık yok (depodaki sürüm 3.5.9-r0), `apk add` "breaks: world[...]" hatasıyla duruyordu. Yığın 30 saat önceki önbellekten çalışıyordu, bu yüzden eski kodu çalıştırıyordu.

Yapılan: `deploy/docker/Dockerfile` içindeki iki sabit satırı 3.5.9-r0 yapıldı (taban imaj özetlerine dokunulmadı).

Kabul koşulları
1. Üç imajın derlemesi hatasız bitti; yığın yeniden kalktı ve sağlıklı (test-runs.log). Derleme, V1-RMD-574 lint düzeltmesi olmadan da ikinci bir engelde duruyordu.
2. Bu klasör.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Sabitler elle güncellendi; depo bir sonraki yama sürümüne geçince aynı hata tekrar eder (sabit tutma politikası bilerek korundu). Bunu uyaran bir denetim yok.
- Güvenlik tarayıcısıyla imaj taraması yapılmadı.
