# Restaurant POS Development Plan

Bu klasör, ürün geliştirmesini sürüm -> modül -> tek kişilik iş sırasıyla böler.
Her modül ayrı klasördür. Her iş ayrı bir Markdown dosyasıdır ve yalnızca bir
kişiye atanır. Aynı görevin testi, migration'ı ve kısa teknik açıklaması o
teslimatın tamamlanma kanıtıdır; bağımsız ikinci bir özellik değildir.

## Sürüm sırası

1. `v0` - Domain kapanışı, dış bağımlılık doğrulaması ve uygulanabilirlik kapısı
2. `v1` - Temel restoran operasyonu
3. `v1.1` - Menü, reçete, üretim ve stok
4. `v1.2` - Ödeme, mali belge, kasa ve meal card
5. `v1.3` - Cari hesap, periyodik faturalama, QNB ve gelen fatura
6. `v1.4` - QR ve online sipariş kanalları
7. `v1.5` - Güvenlik, dayanıklılık, mutabakat ve operasyonel olgunluk
8. `v2.0` - Üretim kabulü ve kontrollü canlıya geçiş

## Kullanım akışı

1. Bir sürümün `README.md` giriş koşulları doğrulanır.
2. Bağımlılıkları kapanmış tek bir görev kodu seçilir.
3. Yalnızca o görevin tarif ettiği davranış uygulanır.
4. Görevde istenen test ve operasyonel kanıt üretilir.
5. Kanıt görülmeden durum `Done` yapılmaz.
6. Sürüm çıkış kapısı kapanmadan sonraki sürüm başlatılmaz.

## Durum değerleri

- `Planned`: Henüz başlanmadı.
- `InProgress`: Aktif olarak yürütülüyor; aynı geliştirici aynı anda ikinci bir
  görev kodu açmaz.
- `Blocked`: Somut dış bağımlılık veya karara bağlı engel var.
- `Done`: Kabul kanıtı gerçek komut, test, migration veya imzalı entegrasyon
  çıktısıyla doğrulandı.

Görev yazım standardı `TASK_STANDARD.md`, sahiplik kuralları `OWNERSHIP.md`,
sürüm kapıları `GATES.md`, PDF denetimiyle bulgu izlenebilirliği ise
`TRACEABILITY.md` içindedir. Denetlenen dosyanın değişmez kimliği
`PDF_SOURCE.md` içindedir.

Kanıtsız davranış üretmeme kuralı `ASSUMPTION_POLICY.md`, PDF bölüm/modül
karşılaştırması `PDF_COVERAGE.md` dosyasındadır.
