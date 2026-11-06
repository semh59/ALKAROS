# V1-RMD-842 sonuç

Kapatılan: `comp` ucu, `bills.comp` yetkisi olmayan kullanıcı için yetki talebinden önce kalem kontrolünü her istekte çalıştırıyordu; onaylanıp uygulanmış ikramın aynı anahtarla tekrar gönderiminde sipariş/hesap arada kapanmışsa 409 dönüyordu. Artık anahtara kayıtlı yetki VE kalemin zaten ikram edilmiş olması birlikte varsa ön kontrol atlanıyor (cevap saklanan sonuçtan geliyor); yeni anahtarda ve henüz uygulanmamış (bekleyen) yetkide kontrol yetki talebinden önce çalışıyor. Ortak yardımcı yok: `void-sent` ucundaki ön kontrol farklı bir doğrulama, tek satırlık koşul uçlarda ayrı.

Ek bulgu (aynı davranışın parçası): uygulanmış ikram kalemin tutarını sıfırlıyor; tekrar gönderim yetki talebini yeni tutarla (0) yaptığı için sipariş açık olsa bile `IDEMPOTENCY_KEY_REUSED` (409) dönüyordu. Uygulanmış ikramın tekrarında onaylanan tutar (kayıtlı yetkideki tutar) kullanılıyor.

## Doğrulama
- Kırmızı (`red.log`): yeni teori her iki hâlde de düştü (sipariş açık / kapanmış).
- Yeşil (`green.log`): Comp HTTP projesi 29/29 (kişisel bütçe tekrarı testi doğrulama turundan).
- Mutasyon (`mutations.log`): uygulanmış ikramda da ön kontrol (1 test düştü), tekrar gönderimde güncel kalem tutarı (2), kayıtlı her yetkide ön kontrol atlanır (1), yeni anahtarda ön kontrol atlanır (4); beşi `cmp` ile geri doğrulandı. Derlenmeyen ilk deneme (`if (false)`, ulaşılamayan kod hatası) geçerli biçimiyle yeniden koşuldu.
- Kapsam aracı exit 0 (`scope.log`). Atlanan test yok.

## İnceleme
qa-backend: Kritik/Yüksek yok. Orta bulgu: ilk sürüm ön kontrolü kayıtlı her yetkide atlıyor ve tutarı yetkiden alıyordu; bekleyen yetki sonradan kapanan kalem için onaylanabilir ve kalem tutarı değişmişse uygulama onaylanan tutardan farklı olabilirdi. Düzeltildi: atlama ve kayıtlı tutar yalnız kalem zaten `Complimentary` iken; bekleyen yetkinin tekrarı kapalı kalemde 409 (yeni test + mutasyon m3).
- Doğrulama turu (qa-bulgu-dogrulayici): yeni açık yok; düşük bulgu (kişisel bütçe tekrarı testsiz) giderildi, mutasyon m5 ile sınandı. Kabul edilen: yetki servisi anahtarı ikinci kez okuyor (arayüz değişikliği kapsam dışı).
