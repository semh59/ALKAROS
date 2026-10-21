# V1-RMD-678 kanıtı

- Bekleyen sipariş turu kuyruğa alan garsonun kimliğiyle etiketlenir (`ownerUserId`); gönderim ve yeniden deneme yalnız oturumdaki garsonun (ya da etiketsiz eski) turlarını alır; başka garsonun turu silinmez, gönderilmez.
- Testler: test-staticapps.log (12 dosya, 110 test yeşil). Mutasyon: mutations.log, 4/4 yakalandı.
- Doğrulanamadı: kuyruğa alma anındaki etiketleme (queueOrder) ayrı bir testle değil yalnız kod okumasıyla doğrulandı (sendDraft yolu tam uygulama DOM-u ister); tarayıcıda elle deneme yapılmadı.
