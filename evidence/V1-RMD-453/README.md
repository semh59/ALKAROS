# V1-RMD-453 - kabul kanıtı

Görev dosyasındaki kabul ölçütlerinin karşılandığını gösteren özet; komut çıktıları bu klasördedir.

- Profil testleri (gerçek PostgreSQL 18, 60/60): VKN sağlama hanesi, TCKN 10. ve 11. hane kuralları, uzunluk, VKN'de
  vergi dairesi zorunluluğu; yönetici tam numarayı, kasiyer yalnız son üç haneyi görür, diğer roller hiçbir şey görmez;
  numara veritabanında düz metin olarak yer almaz; güncelleme değiştirir ve kaldırır; anonimleştirme vergi kimliğini de
  siler; maskeli değer geri yazılamaz; eski kayıtlar vergi kimliği olmadan okunur. Sağlama kontrolü devre dışı
  bırakılınca üç test kırmızı (`evidence/V1-RMD-453/mutation-vkn-checksum.log`).
- HTTP testleri (19/19): VKN ile oluşturulan müşteri yanıtta ve listede maskeli; sekiz hatalı girdi Türkçe gerekçeyle
  400 ve kayıt oluşmaz; `PUT /api/v1/terminals/{terminalId}/customers/{customerId}/tax-identity` vergi kimliğini
  koyar, değiştirir, kaldırır ve diğer alanları korur; bilinmeyen müşteri 404; yetkisiz 403. Anonimleştirme, hesaba
  yazma ve uç yetkilendirme paketleri yeşil (`evidence/V1-RMD-453/dotnet-test.log`).
- Kasiyer E2E (3/3): hatalı VKN Türkçe gerekçeyle reddedilir, doğrusu ile müşteri eklenir, kayıt listede ve ekstrede
  `VKN *******890 · Kadıköy V.D.` olarak görünür, tam numara sayfada yoktur; TCKN'ye geçiş kaydedilir
  (`evidence/V1-RMD-453/e2e-customer-accounts.log`).
- Semih'in elle deneyebileceği senaryo: Kasa → Cari Hesaplar'da yeni müşteri için "VKN (şirket)" seçip 1234567891 ve
  bir vergi dairesi girin; "Vergi kimlik numarası geçersiz; rakamları kontrol edin." uyarısını görün. 1234567890 ile
  ekleyin; listede `VKN *******890` görünür. Müşteriyi açıp "Fatura için vergi kimliği" alanından TCKN 10000000146
  girip "Vergi kimliğini kaydet"e basın.
