# V1-RMD-650 kanıtı

- Kırmızı: `red.log` — eski kodda denetim yazımı reddedilince ölü mektup yine de `pending` oluyor.
- Yeşil: `test-runs.log` (SecurityAdministration testleri, temiz derleme, 0 uyarı); yeni test `green-new.log`.
- Test tekniği: veritabanında belirli bir denetim olayını reddeden geçici tetikleyici; ölü mektup `dead` kalır, gizli anahtar durumu (başlat, döndür, iptal, geri al) değişmez, hiçbir sipariş kapatılmaz.
- Mutasyon: `mutations.log` — 5/6 yakalandı. Hayatta kalan "requeue audit not in the update transaction": denetim ayrı bağlantıda ama commit'ten önce yazıldığı için denetim hata verirse yine geri alma oluyor; test edilen özellik (denetim yazılamazsa eylem yok) için eşdeğer. Fark: commit'in kendisi sonradan hata verirse eylem olmadan denetim satırı kalabilir (iz fazlası, iz kaybı değil).
- Gizli anahtar kaydı veritabanında olmadığı için denetim eylemden önce yazılır; Save başarısız olursa denetimde yapılmamış bir deneme izi kalır (kabul edilen yön).
- Kalan risk: gözlemlenebilirlik dedektörleri, saklama servisleri, anonimleştirme, tanılama paketi ve şüpheli giriş kaydı ayrı görevlerde.
