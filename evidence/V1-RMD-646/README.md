# V1-RMD-646 kanıtı

- Kırmızı: `red.log` — düzeltme geri alınmışken iki atomiklik testi başarısız (iptal ve iade reddi).
- Yeşil: `test-runs.log` — Billing testleri ve Audit testleri (temiz derleme sonrası) başarılı.
- Mutasyon: `mutations.log` (5 yakalandı, 1 eşdeğer), `mutate.mjs`.
- Kalan risk: kalan denetim yazımı çağrı noktaları (indirim, bahşiş, sipariş kalemi, çift ekran, diğer modüller, ham INSERT'ler) hâlâ işlemden sonra ayrı bağlantıda yazıyor; ayrı görevlerle taşınacak. `IAuditEventStore` işlem içi aşırı yükleme varsayılanı desteklemeyen mağazada hata verir (sessizce dışarı yazmaz).
