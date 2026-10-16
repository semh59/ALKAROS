# V1-RMD-579 - Yönetim panelinde performans bütçesi ekranı

Yönetim > "Hız sınırları" sekmesi (rapor görüntüleme yetkisiyle görünür): bütçe listesi, varsayılan istek sınırı notu, ekleme/güncelleme formu, düzenle ve sil düğmeleri. Düzenleme araçları yalnız `observability.manage` yetkisinde görünür. Tüm metinler Türkçe; ölçüm türleri Türkçe etiketle gösterilir.

Kanıt: `test-runs.log` (PosTerminal 75 dosya / 639 test yeşil, `tsc` 0, oxlint çıkış 0), `mutations.log` (8/8 mutant öldürüldü; boşluk-yalnız ad mutantı ilk koşuda yaşadı, test eklenince öldü).

Sınırlar (doğrulanamadı)
- Gerçek sunucu ve tarayıcıyla uçtan uca denenmedi; bileşen testi sahte istemci kullanır, uç nokta sözleşmesi V1-RMD-570 testlerinde doğrulandı.
- Sınır birimi: istek ve veritabanı komutu için milisaniye; kuyruk derinliği ve ekran etkileşimi için birim ekranda belirtilmedi (yalnız varsayılan istek notunda "ms" yazar).
- Ekran görüntüsü alınmadı.
