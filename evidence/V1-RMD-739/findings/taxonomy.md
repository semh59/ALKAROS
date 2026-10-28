# Hata kategorileri (bir madde tam bir kategoriye girer)

| Kod | Kategori | Ne girer |
|---|---|---|
| EZ | Eşzamanlılık ve atomiklik | yarış, kilitsiz oku-sonra-yaz, çift uygulama, işlem dışı çok adımlı yazma |
| TE | Teslimat eksik / bağlı değil | Done denen ama yapılmamış, hiçbir yerden çağrılmayan kod, eksik uç nokta veya ekran |
| KN | Kanıt ve test eksik | kanıtsız Done, günlük/kanıt klasörü yok, kabulü sınamayan test, test boşluğu |
| BD | Bayat belge | md/README kodla uyuşmuyor, silinmiş dosyaya atıf, yanlış sayı |
| PO | İş kuralı ve mevzuat belirsizliği | Semih kararı gereken: KDV, ÖKC, saklama süresi, vardiya günü, yaş kısıtı |
| UI | Arayüz | Türkçe metin, ham hata, durumlar, erişilebilirlik, tasarım tutarlılığı, akış |
| VD | Veri doğruluğu ve doğrulama | yanlış hesap, sınır, eksik girdi doğrulaması, yuvarlama, tip kaybı |
| GY | Güvenlik, yetki, KVKK | yetki boşluğu, gizlilik, saklama, denetim izi |
| DY | Dayanıklılık ve hata işleme | yutulan hata, yeniden deneme, zaman aşımı, performans, sınırsız sorgu |
| DG | Diğer | yukarıdakilere girmeyen |

Kural: birden fazla uygunsa maddenin ASIL kusuru olan kategori seçilir.
