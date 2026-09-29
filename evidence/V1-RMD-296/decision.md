# V1-RMD-296 karar kaydı: yönetici ekranları tek "Yönetim" alanında

- Karar sahibi: Semih
- Tarih: 2026-09-29
- Soru: İstemcisi olmayan yönetici uçları nerede ve hangi sırayla ekrana gelsin?
- Cevap: PosTerminal'de tek bir "Yönetim" sekmesi, içinde rol tabanlı bölümler; öncelik "para ve kapanış önce".

## Gezinme modeli

- PosTerminal çalışma alanının gezinme çubuğuna tek bir "Yönetim" öğesi eklenir. Oturum aşağıdaki bölümlerden en az
  birinin iznine sahipse görünür.
- Yönetim alanı içinde bölümler sekme olarak sıralanır. Oturumun izni olmayan bölüm listelenmez; izni olmayan işlem
  düğmesi gösterilmez. Sunucu her uçta izni yeniden denetler; sunucunun Türkçe hata gerekçesi ekranda aynen görünür.
- Mevcut ayrı ekranlar (işletme kimliği, müşteri ekranı, QNB, röle, Token terminali, çevrimiçi yemek) şimdilik
  yerinde kalır; Güvenlik Yönetimi ekranı Güvenlik ve sistem bölümüne taşınır.

## Bölümler, öncelik sırası ve yetki matrisi

| Sıra | Görev | Bölüm | Uçlar | İzin |
| --- | --- | --- | --- | --- |
| 1 | V1-RMD-445 | Alan kabuğu + Gün sonu ve mutabakat | `reporting/business-day`, `payments`, `reconciliation/cases` | `reports.view`, `reports.close-day`, `reconciliation.manage` |
| 2 | V1-RMD-446 | Stok | `inventory`, `inventory/reports` | `inventory.manage`, `reports.view` |
| 3 | V1-RMD-447 | Satın alma ve üretim | `purchasing`, `production` | `purchasing.manage`, `production.manage` |
| 4 | V1-RMD-448 | Menüler ve tarif maliyeti | `menus-and-specials`, `recipes/{id}/cost-snapshots` | `menu.manage`, `inventory.manage` |
| 5 | V1-RMD-449 | Personel ve roller | `roles`, `users` | yönetici/şef garson oturumu; komut başına `identity.*.manage` |
| 6 | V1-RMD-450 | Güvenlik ve sistem | `security`, `observability` | `security.manage`, `observability.manage`, `reports.view` |
| 7 | V1-RMD-451 | Ayar geçmişi | `settings` | `settings.manage` |

Tüm uçlar `/api/v1/management/` altındadır. Her bölüm ayrı bir uygulama görevidir; 446–451, 445'in kurduğu kabuğa
bağlıdır.

## Neden bu sıra

Para ve kapanış (gün sonu, ödeme mutabakatı, vakalar) bugün yalnız API üzerinden yapılabiliyor ve günlük
kapanışı engelliyor; stok ve satın alma haftalık işler; personel, güvenlik ve ayar geçmişi seyrek kullanılır.
