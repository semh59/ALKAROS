# V1-RMD-006 iki ekran browser E2E kanıtı

- Tarih: 2026-08-24
- Cashier origin: `http://localhost:5080/`
- Customer display origin: `http://127.0.0.1:5080/display`
- Kalıcı kaynak: PostgreSQL 18.6

İki farklı origin kullanılarak cashier ve customer display browser storage alanları ayrıldı.

## Başarılı akış

1. Cashier gerçek Identity kaydıyla giriş yaptı ve gerçek Catalog ürünlerini aldı.
2. Customer display sekiz karakterli kod ve tek kullanımlık secret ile pairing başlattı.
3. Cashier kodu onayladı; display HttpOnly session cookie aldı.
4. Sipariş `v1` olarak açıldı.
5. Mercimek ürünü eklenince revision `v2` oldu ve display yeni snapshot'ı gösterdi.
6. Miktar artırılınca revision `v3` oldu ve toplam değişti.
7. Satır silinince revision `v4` oldu ve müşteri ekranından kaldırıldı.
8. Burger eklenince revision `v5` oldu.
9. Submit sonrasında revision `v6` oldu ve cashier düzenleme kontrolleri kapandı.
10. `Sonraki siparişi aç` ile farklı order number taşıyan yeni Draft sipariş açıldı.
11. Yeni siparişte ürün kartları yeniden etkinleşti; önceki Submitted sipariş PostgreSQL'de korundu.

## Yenilenen kasa arayüzü

- Kategoriler frontend sabiti değil, gerçek `catalog.categories` join sonucundan oluşturuldu.
- Kategori seçimi yalnız ilgili gerçek ürünleri gösterdi.
- `sufle` araması tek gerçek Catalog kaydı döndürdü.
- Kasa ve müşteri ekranında birim fiyat ile satır toplamı KDV dahil ve matematiksel olarak uyumlu gösterildi.
- Sabit `PostgreSQL bağlı` metni kaldırıldı; başlık `/health/ready` sonucuna göre değişiyor.
- Dekoratif veya çalışmayan menü eklenmedi; görünen bütün kasa aksiyonları gerçek endpoint çağırıyor.

## Restart, reconnect ve stale

- Host durdurulup yeniden başlatıldı.
- Her iki client reload sonrasında açık siparişi browser belleğinden değil PostgreSQL'den `v6` olarak geri aldı.
- Cashier oturum adı `/auth/session` üzerinden `Test Kasiyer` olarak geri yüklendi.
- SignalR kesintisinde beş saniyelik HTTP reconciliation snapshot'ı korudu.
- Host on saniyeden uzun kapalı tutulunca display eski ürünleri ve tutarları temizledi.
- Stale görünümünde yalnız `Bilgi güncellenemiyor` mesajı kaldı.
- Host geri geldiğinde `v6` snapshot yeniden yüklendi.
- Restart sonrasında yapılan yeni satır değişikliği müşteri ekranına `1367 ms` içinde ulaştı; beş saniyelik polling
  süresinden kısa olduğu için SignalR reconnect yolu da doğrulandı.

## Session revoke

- Cashier aktif display session'ı revoke etti.
- Eski display cookie ile snapshot isteği `401` döndü.
- Customer display eski siparişi göstermeyi bıraktı ve otomatik olarak yeni pairing kodu üretti.

## Veri sınırı

Customer snapshot DTO'sunda yalnız müşteri ekranının kullandığı terminal, sipariş revision, durum, satır adı, miktar
ve parasal toplamlar bulunur. Personel kimliği, session token, pairing secret, provider alanı, mali cihaz alanı ve
dahili not taşınmaz.
