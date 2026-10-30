# Hata ve eksik sınıflandırması

**Bütün gerçek hata ve eksikler kritik.** Eski Yüksek/Orta/Düşük etiketleri yalnız kaynak tarihçesi; hiçbir gerçek kusur küçüklüğü nedeniyle elenmez.

## Sonuç

- **1.376 kaynak kaydı sınıflandırıldı: 1.020 ana + 356 ek.** Bağımsız ve açık hata sayısı değildir.
- Her kaydın kimliği, ana/ikincil kategorisi, kaynak dosya:satırı, özgün metni, gerekçesi, kapanış durumu ve yönlendirme paketi var.
- **300 kategori/alan yönlendirme paketi** ve **5 kaynakla desteklenen kök neden/örtüşme bağı** oluşturuldu. Paketler uygulama görevi veya kesin ortak kök neden değildir.
- Eski ana kategorisi değiştirilen: **162**; kategori torbası “Diğer”: **0**.
- Ana kayıt nitelikleri: 908 aday; 81 belirsiz; 28 bilgi; 3 bulgu-degil.
- Ek kayıt nitelikleri: 183 belirsiz; 152 aday; 19 bilgi; 2 bulgu-degil.
- Aday = kaynaktaki somut kusur iddiası; belirsiz = araştırma/karar bekliyor. Bilgi ve bulgu-değil ayrı tutulur, yok edilmez.

## Kategoriler (ana + ek = kaynak kayıt toplamı)

- **KN — Kanıt ve test boşluğu: 286 + 68 = 354**
- **BD — Belge ve izlenebilirlik: 286 + 9 = 295**
- **PO — Ürün, iş kuralı ve mevzuat: 39 + 129 = 168**
- **UI — Arayüz ve erişilebilirlik: 30 + 98 = 128**
- **TE — Eksik teslimat ve entegrasyon: 84 + 19 = 103**
- **VD — Veri doğruluğu ve doğrulama: 85 + 12 = 97**
- **DY — Dayanıklılık ve performans: 69 + 10 = 79**
- **EZ — Eşzamanlılık ve atomiklik: 52 + 4 = 56**
- **GY — Güvenlik, yetki ve gizlilik: 48 + 7 = 55**
- **BK — Kod kalitesi ve yasaklı uygulama: 28 + 0 = 28**
- **MK — Mimari sınır ve sahiplik: 13 + 0 = 13**

## Kapanış durumu

- **16** ana kayıt için önceki kod/günlük incelemesine dayalı tam kapanış eşleşmesi; **29** kayıt kısmi. Hiçbiri yeni test koşusu diye sunulmaz.
- Üç açık “bulgu değil” kaynak notu korunur. Önceki “1.000 kalan” formülü kesin açık hata sayısı değildir; bilgi/belirsizlik, tekrar ve bileşik kusurlar nedeniyle böyle bir toplam verilmedi.
- Kaynak incelemesi ve kapanış eşleşmesi HEAD `05d2fffa` dönemine dayanıyor. Sınıflama üretiminde HEAD `8c03e79b`; aradaki V1-RMD-750 başka oturum değişikliği. Yeni HEAD’de 17 düzeltmenin bütün testleri yeniden çalıştırılmadı.

## Ayrı inceleme envanteri

- **522** tarama eşleşmesi; tek başına kusur kanıtı değil. “InMemory”, zaman veya `.message` adı/deseni üzerinden kesin hüküm yok.
- **974** tarihsel kanıt bayraklı görev; bayraklar çakışabilir, güncel kapanışı kendiliğinden iptal etmez.
- **42** kanıtsız bayat belge yolu; ana ve ek kayıtlarla örtüşebilir.
- Karar çıkarıcısında 1.243 batch görevine karşı 1.232 etiket var; 12 exact karar kimliği eksik, bir birleşik `V0-REV-001..004` etiketi var. Bu kaynak defter açığı korunur; kartların tamamlanmadığına tek başına kanıt değildir.

## Kullanılacak dosyalar

- `TUM_KAYITLAR.csv`: 1.376 kaynak kaydın tamamı; kaynak ve gerekçe korunur.
- `PAKETLER.md` ve `DUZELTME_PAKETLERI.json`: yönlendirme, kabul, karar ve kapsam kapıları.
- `KOK_NEDEN_BAGLARI.json`: kanıtlı örtüşmeler; diğer tekrarlar henüz kesin birleştirilmedi.
- `POLITIKA.md`: kritiklik, yasaklı uygulama, kategori ve kanıt kuralları.
- `ANA_KAYITLAR.json`, `EK_KAYITLAR.json`, `KAPANIS_ESLESMESI.json`: ayrıntılı kaynak/kapanış defteri.
- `SAYIM.json`: sayımlar ve SHA-256 kaynak envanteri.
- `SINIFLAMA_DUZELTMELERI.json`: parent incelemesinde yanlış dışlama/gerekçeye uygulanan düzeltmeler.
- `validate.py`: kimlik, kategori, öncelik, kaynak metni/hash, paket kapsamı ve yazma sınırı kontrolü.

## Kalan iş ve iddia sınırı

Bu teslimat mevcut kayıtların kategorize envanteridir; bütün uygulamanın yeniden denetlendiği veya mevcut bütün kusurların çalıştırılarak doğrulandığı iddia edilmez.
Kategori/alan paketleri her bağımsız davranış için ayrı task ve exact allowlist’e dönüştürülmeden kod yazılmaz. Bileşik kayıtlar alt kusurlara ayrılıp, örtüşmeler ve güncel kapanış kanıtları eşleştirilmeden kesin açık kök neden toplamı verilemez.
Varsayımsal/hayali kod, teknik borç ve ölü kod kabul edilmez. Eski izin listesi tek başına muafiyet değildir; dış servis/cihaz eksikliği uydurma entegrasyonla kapatılmaz.
