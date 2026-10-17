# V1-RMD-582 - QNB e-Arşiv fatura gönderim istemcisi

Neden: online sipariş faturalarının alıcısı e-Fatura mükellefi olmayan müşteridir, bu yüzden e-Arşiv gerekir; repoda yalnız e-Fatura taslağı vardı ve e-Arşiv için görev yoktu.

Eklenen: `src/Modules/Invoicing/Qnb/EArsiv/QnbEArsivClient.cs`. QNB'nin herkese açık WSDL/XSD'sine (https://earsivtest.qnbesolutions.com.tr/earsiv/ws/EarsivWebService?wsdl, ad alanı http://service.earsiv.uut.cs.com.tr/) ve API belgesindeki "e-Arşiv Fatura Gönderme" sayfasına göre:
- Giriş e-Arşiv sunucusunun kendi `userService` adresinde yapılır (e-Fatura adresinden türetilmez).
- `faturaOlusturExt`: `input` JSON (`islemId`, `vkn`, `sube`, `kasa`, `erpKodu`, `donenBelgeFormati` "9") ve `fatura` (`belgeFormati` UBL, `belgeIcerigi` base64).
- Sonuç kodu `AE00000` değilse kod ve metinle hata; başarıda `faturaNo`, `uuid`, `faturaURL` okunur, numara veya UUID boşsa hata.
- Ortak SOAP çağrısı için `QnbSoapClient`e iki iç yardımcı eklendi (`CallAsync`, `EscapeXml`).

Kanıt: `test-runs.log` (Qnb.Client testleri 25/25), `mutations.log` (9 mutantın 8'i öldürüldü, 1 eşdeğer mutant açıklandı).

Sınırlar (doğrulanamadı)
- Gerçek QNB hesabıyla denenmedi (V0-QNB-001 Blocked: test formu ve `erpKodu` sahibin başvurusuna bağlı). İstek ve cevap biçimi yalnız yayımlı WSDL/XSD ve belge sayfasındaki örnekten alındı.
- `donenBelgeFormati` değeri belgenin örneğindeki gibi metin olarak gönderilir (parametre tablosu "int" diyor); gerçek sunucuda doğrulanmalı.
- Sorgulama, iptal, itiraz ve rapor gönderme yapılmadı: bu çağrıların giriş JSON şeması QNB belgesinde yayımlanmamış.
- UBL-TR fatura XML'ini üreten kod bu görevde yok (V1-RMD-470..475 zinciri); e-Arşiv raporunu QNB'nin mi bizim mi gönderdiği bilinmiyor (QNB'ye sorulacak).
- Taslaktaki e-Fatura gönderim hatası (`belgeOid` yerine `return` okuma, `gonderenEtiket` eksikliği) bu görevde düzeltilmedi; o kod `evidence/V13-GOV-007/` altında kapalı bir görevin taslağıdır ve V14-QNB-002 başlayınca yeniden yazılacaktır.
