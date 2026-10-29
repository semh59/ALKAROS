# V1-RMD-755 — doğrulama özeti (kapanış henüz bekliyor)

- Eski V1-RMD-753 Done beyanı geçersiz: gerçek kaynak derlemesi iki CS1503 verdi; `build-before-fix.log`.
- Named adjustment çağrısı ve iki hedefli null senaryoları düzeltildi. Aynı davranışlar parametreli sekiz senaryoda indirim, bahşiş, ücret ve null toplamlarıyla test edildi.
- SplitDesign tam süiti: 40/40, exit 0. Sekiz yeni senaryonun her biri TRX içinde geçti.
- Host Billing tam süiti: 112/112, exit 0. İndirimli Equal/Custom kayıt ve tekrar okuma; hem tutar hem vergi değişmemiş net-sıfır senaryoda tam kalem bölme başarısı kanıtlandı.
- V1-RMD-754 regresyon metodu korunuyor; vergi değişmiş net-sıfır senaryoda 409 bekleniyor.
- Build: iki proje exit 0, 0 hata/0 uyarı (`build-results.json`).
- İzole kopyada dokuz ayrı mutant çalıştırıldı ve davranış hatasıyla yakalandı; geri yükleme sonrası Domain 8/8 ve Host 3/3 geçti. Canlı üretim dosyasına yazılmadı (`guard-mutation-results.json`).
- TRX, yeni test adları, exit ve log hash'leri: 88/88 kontrol geçti (`evidence-verification.json`).
- GUID sırasına bağlı eski testte iki taraf da sıralandı; üyelik/adet/multiplicity ve yetkisiz sandalye ret denetimi korundu.
- Başlangıçtaki 262 dirty kayıt ve tüm kaynak/index snapshot'ı karşılaştırıldı: görev dışı 8.784 dosya değişmedi; staged diff boş kaldı. Metadata yalnız 753 Blocked + zorunlu Blocker.
- Plan, consistency ve CRLF-uyumlu whitespace kontrolleri exit 0. Pending-delta allowlist temiz. Bu sonuç henüz gerçek committed-range task-scope ve bağımsız son inceleme yerine geçmez.
- Kaynakta üretim davranışı değişmedi. 753 tarihsel kontratı değişmedi; A kararıyla çelişen eski başarı beklentisi tarihsel bırakıldı ve blocker içinde açıklandı.

## Açık kapanış kapıları

İki bağımsız son inceleme, ardından stage içerik/index doğrulaması, nonempty görev-local commit aralığı task-scope ve son durum kaydı. Bunlar geçmeden Done yok.

## Elle senaryo

İndirimli hesabı kişi/serbest böl; kaydı tekrar açınca indirimli tutar/vergi ve paylar korunmalı. Bahşiş ve aynı tutarlı indirim vergiyi değiştirirse ürün bölme 409 verir. Vergisi sıfır olan hesapta aynı iki hareket hem payable hem tax değerini korursa tam ürün miktarlarıyla bölme kaydedilir.
