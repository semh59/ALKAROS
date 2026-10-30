# Bulgu sınıflandırma ve kapanış politikası

## Bağlayıcı karar

Semih'in 2026-10-28 tarihli talebi: **en küçük gerçek hata ve eksik dahi kritiktir**. Önceki Yüksek/Orta/Düşük etiketleri yalnız kaynak tarihçesidir; kapsam dışına bırakma, erteleme veya kabul gerekçesi değildir. Varsayımsal/hayali uygulama, teknik borç, ölü/ulaşılamayan kod, placeholder, mock-success ve kanıtsız fallback kabul edilmez.

Kategori, kritiklikten ve kanıt durumundan bağımsızdır. Bir kayda tek ana kategori, gerektiğinde ikincil kategoriler atanır. “Diğer” torbası kullanılmaz.

## Kategoriler

- **EZ — Eşzamanlılık ve atomiklik:** yarış, işlem sınırı, kilit, idempotency ve çift uygulama.
- **TE — Eksik teslimat ve entegrasyon:** olmayan/bağlanmamış uç, ekran, tüketici, zamanlayıcı ve kabul davranışı.
- **KN — Kanıt ve test boşluğu:** çalıştırılmamış/eksik kabul, kırık veya zayıf test, mutasyon ve gerçek cihaz/sağlayıcı kanıtı eksikliği.
- **BD — Belge ve izlenebilirlik:** bayat Markdown, yanlış sayı/yol, eski karar, kapanış eşleştirmesi ve eksik kayıt.
- **PO — Ürün, iş kuralı ve mevzuat:** Semih veya yetkili uzman kararı gerektiren belirsizlik. Yeni özellik isteği, mevcut kapsam kanıtlanmadan uygulama kusuru sayılmaz.
- **UI — Arayüz ve erişilebilirlik:** Türkçe, terminoloji, görünür durumlar, odak, dokunma, tasarım ve kullanım akışı.
- **VD — Veri doğruluğu ve doğrulama:** yanlış hesap, para/yuvarlama, birim ve girdi sınırları.
- **GY — Güvenlik, yetki ve gizlilik:** yetki kontrolü, token/sır sızıntısı, KVKK ve denetim izi.
- **DY — Dayanıklılık ve performans:** hata/kurtarma, timeout/retry, sınırsız sorgu ve ölçülmüş performans kusuru.
- **MK — Mimari sınır ve sahiplik:** modül sınırı, görev yüzeyi, ortak şema/yapı sahipliği.
- **BK — Kod kalitesi ve yasaklı uygulama:** kanıtlanmış gereksiz tekrar/katman, varsayımsal veya hayali uygulama, teknik borç, ölü kod ve bastırma.

## Kanıt ve kayıt durumları

- **aday:** kaynaktaki somut kusur iddiası; bu sınıflama tek başına güncel çalışma kanıtı değildir.
- **belirsiz:** kaynak açıkça doğrulayamadığını söylüyor veya karar/kapsam/etki belirsiz; kapatılmaz, araştırma işi olarak izlenir.
- **bilgi:** tarihçe, bilinçli ve onaylı davranış veya kendi başına kusur göstermeyen not. Gerçek kusur bulunursa aday kaydına dönüştürülür.
- **bulgu-degil:** kaynak açıkça bulgu olmadığını veya iddianın çürütüldüğünü belirtir; sayıdan sessizce silinmez, gerekçesi korunur.

Kapanış ayrıca tutulur. Aynı Task ID'yi taşıyan bütün satırlar, bir düzeltme yapıldı diye topluca kapanmaz. Bir satırdaki bütün kusurlar giderilmediyse kısmi kalır. Test/README iddiası gerçek komut günlüğüyle ayrılır. Eski kanıt, yeni HEAD'deki tüm koşulların doğrulandığı anlamına gelmez.

## Sayım birimi ve kapsam

1. Ana kaynaklar: `findings/register.md` ve `low-A.md`, `low-B.md`, `low-C.md`: **1.020 kaynak kaydı**; 45/300/675 tarihsel önem dağılımı. Kayıt sayısı, bağımsız ve açık hata sayısı değildir.
2. Ana kayıt kimlikleri kaynak sırasıyla `F0001`–`F1020`; kaynak dosya ve satır korunur. Bileşik satır, düzeltme planlamasında alt kusurlara bölünmeden tekil kök neden sayısı verilemez.
3. Yedi `ui/*.md` raporu, 47 `po-questions.md` satırı, pilot `V1-RMD-604/README.md` ve `V1-RMD-737/README.md` kalanları ayrıca kapsanır. Olası tekrarlar silinmez veya otomatik toplanıp tekil hata sayılmaz.
4. `scans/*.txt` eşleşmeleri ve `ledger.jsonl` bayrakları ayrı inceleme adayı envanteridir. Örneğin `DateTime.Now`, `InMemory` veya ham `.message` eşleşmesi tek başına kanıtlanmış kusur değildir. Sıfır metin eşleşmesi de o kusurun depoda bulunmadığını kanıtlamaz.
5. 42 kanıtsız bayat belge yolu ve kaynak kapsamındaki eksik kararlar görünür tutulur. Aynı maddelerle örtüşebileceği için kör toplam yapılmaz.
6. Bu çıktı mevcut kayıtların sınıflandırmasıdır; bütün uygulamanın yeniden satır satır incelendiği veya tüm hataların bulunduğu iddiası değildir.

## Bu çalışmanın yazma sınırı

- Repository: `D:/PROJECT/ALKAROS`
- Aktif Task ID: `V1-RMD-739` (mevcut InProgress doğrulama programının belge/kanıt devamı; yeni uygulama görevi başlatılmadı).
- Assignee: `hermes-session-20261028_015834_6948d340`.
- Başlangıç HEAD: `05d2fffa4563e7813ebafa337f6e5f5d151be86c`.
- Başlangıç `git status --short` ve `git diff --name-only`: boş; iki komutun exit code değeri 0.
- Bu çalışma için daha dar allowlist: `evidence/V1-RMD-739/classification/**` ve yalnız `plan/v1/remediation/V1-RMD-739-bitmis-gorev-dogrulama-programi.md` içindeki Assignee metadata satırı.
- Üretim kodu, uygulama testi, bağımlılık, migration ve diğer görev dosyaları değiştirilmez. Silme, taşıma, commit ve push yapılmaz.
