# ALKAROS Kasa Arayüz Kuralları

Bu belge kasa ekranlarının (masa planı, masa paneli, Hesap Ödeme, iade ve iptal) görsel kurallarını
tek yerde toplar. Dil ve hata metni kuralları [UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md), renk ve genel
tasarım temeli [design/foundations.md](design/foundations.md) belgesindedir; burada onlarla çelişen
kural yoktur, yalnız kasa için daha sıkı sınırlar vardır.

Kasiyer önceliği: ekran hızlı ve ıslak elle kullanılır. Bir kural güzellikle çatışırsa hız, okunurluk
ve yanlış dokunmayı önleme kazanır. Yeni renk eklenmez; yalnız aşağıda adı geçen jetonlar kullanılır.

İki istemci aynı kuralı uygular: PosTerminal (React) `--ds-*` jetonlarını, Cashier sayfaları
(`payments/split-payment`, `cashier-app.css`) aynı değerlerin `--color-*` kopyalarını kullanır.

| PosTerminal | Cashier sayfaları | Değer |
| --- | --- | --- |
| `--ds-color-ink` | `--color-ink` | `#0b2135` |
| `--ds-color-brand` | `--color-brand` | `#1b4d7b` |
| `--ds-color-accent` | `--color-accent` | `#00cfff` |
| `--ds-color-muted` | `--color-text-muted` | `#4d5c68` |
| `--ds-color-line` | `--color-border` | `#c8d0d7` |
| `--ds-color-success` | `--color-success` | `#176548` |
| `--ds-color-warning` | `--color-warning` | `#735400` |
| `--ds-color-danger` | `--color-danger` | `#9a2530` |
| `--ds-target-min` | `--touch-target` | `48px` |

Cashier sayfalarında `-soft` zemin karşılığı yoktur; aynı rengin açık tonu (`rgba`) kullanılır.

## 1. Kapsam

Kasiyerin dokunduğu her ekran: masa planı, masa paneli, Hesap Ödeme, iade ve iptal, hızlı satış.
Yönetici ekranları bu belgenin dışındadır; ham kimlik gibi teknik bilgi yalnız orada gösterilir.
Bu belge hedef kuraldır: bir ekran kuraldan ayrışıyorsa ekran düzeltilir, belge değil. Durum tablosu (4) masa planı
durumlarını kapsar; diğer ekranlar aynı jetonları kullanır.

## 2. Punto

- En küçük yazı 14px (`0.875rem`): rozet, alt not, kart altı, etiket, uyarı. 14px'ten küçük yazı yoktur.
- Gövde ve düğme yazısı 16px, tutar ve ana değer 20px ve üstü, ekran başlığı 22px ve üstü.
- Rozet ve etiketler kalın (700) yazılır; renk tek başına anlam taşımaz.

## 3. Dokunma hedefi ve boşluk

- Her dokunulan öğe en az `--ds-target-min` (48px) yüksekliğinde ve genişliğindedir; birincil eylem 56px ve üstü ([design/foundations.md](design/foundations.md) 3).
- Yan yana ve alt alta iki dokunma hedefi arasında en az 16px (`--ds-space-4`) boşluk bırakılır.
- Yıkıcı eylem (iptal, iade) başka eylemlerle aynı satırda durmaz; kendi satırında ve kendi stilindedir.
- Pasif düğmenin yanında neden bir cümleyle yazılır; nedensiz pasif düğme yoktur.

## 4. Durum tablosu

Aynı durum her ekranda aynı rozetle, aynı yazı ve zemin jetonuyla çizilir (kart, panel, liste, rozet).
Renk yetmez: her durumda ikon ve Türkçe etiket de vardır. Anahtarlar masa planındaki plan anahtarlarıdır.

| Anahtar | Etiket | İkon | Yazı jetonu | Zemin jetonu | Neden |
| --- | --- | --- | --- | --- | --- |
| `free` | Müsait | ○ | `--ds-color-success` | `--ds-color-success-soft` | Boş masa, işlem gerekmez |
| `busy` | Dolu | ● | `--ds-color-ink` | `--ds-color-surface-subtle` | Olağan durum bağırmaz, nötrdür |
| `ask` | Hesap istedi | ⚠ | `--ds-color-warning` | `--ds-color-warning-soft` | Misafir bekliyor, kasiyer harekete geçer |
| `pay` | Ödeme bekliyor | ₺ | `--ds-color-info` | `--ds-color-info-soft` | Hesap açık, tahsilat sırada |
| `rez` | Rezerve | ◔ | `--ds-color-brand` | `--ds-color-surface-subtle` | Planlı, dokunulmaz |
| `clean` | Toplanıyor | ✦ | `--ds-color-muted` | `--ds-color-surface-subtle` | Bilgi, uyarı değil |
| `off` | Servis dışı | ⊘ | `--ds-color-disabled-ink` | `--ds-color-surface-subtle` | Kullanılamaz |

- Kırmızı (`--ds-color-danger`) yalnız hata, yıkıcı eylem ve "Uzun süre açık" uyarısı içindir; normal bakiye kırmızı olmaz.
- Uzun süre açık: masa 45 dakikayı (`--ds-heat-active-max-minutes`) aşınca kart "Uzun süre açık" yazısı taşır ve
  ayırt edici deseni yalnız o masa (ve hesap isteyen masa) alır. Diğer masalarda desen yoktur.
- Dikkat şeridi kartları tekrarlamaz; en çok sayı gösterir.

## 5. Düğme hiyerarşisi

| Rol | Görünüm | Kural |
| --- | --- | --- |
| Birincil | `--ds-color-accent` dolgu, `--ds-color-ink` yazı | Ekranda en çok bir tane: o ekranın asıl eylemi |
| İkincil | `--ds-color-surface` zemin, `--ds-color-line` kenar, ink yazı | Yardımcı eylemler |
| Yıkıcı | surface zemin, `--ds-color-danger` yazı ve kenar | Kendi satırında; tek dokunuşla uygulanmaz |
| Yıkıcı onay | `--ds-color-danger` dolgu, beyaz yazı | Yalnız onay adımında |

Beyaz yazı yalnız yıkıcı onayda ve koyu dolguda kullanılır; `--ds-color-accent` üzerinde yazı daima ink'tir.

## 6. Para ve rakam biçimi

- Para her ekranda `₺160,00` biçimindedir: simge önde, virgül ondalık, iki hane. `5 ₺` veya `160.00` yazılmaz.
- Rakamlar `font-variant-numeric: tabular-nums` ile hizalanır.
- Kalan tutar nötrdür (ink); bakiye tamamen ödendiğinde success. Ödenen, Hesap toplamı ile aynı boyda ve ağırlıktadır.

## 7. Başlık düzeni

- Bir ekranda bir başlık (`h1`) vardır; üst etiket, başlık ve alt başlık aynı şeyi tekrar etmez.
- Başlık ekranın eylemini söyler ("Tahsilat"); masa adı ("Masa M-03") onun altında tek bağlam satırıdır ve ikinci bir başlık gibi çizilmez.
- Ham terminal kodu veya kimlik kasa başlığında gösterilmez.

## 8. Yıkıcı işlem onayı

- Onay yalnız geri alınamaz eylemde (iptal, iade, void) istenir; geri alınabilir eylem tek dokunuşla uygulanır ve geri al sunar (foundations 5.2).
- Onay düğmesi eylemi, masayı ve tutarı adlandırır (ör. "M-03 masasına ₺145,00 iade et").
- Uyarı metni 14px veya büyük, ink ya da muted renkte, düğmenin hemen üstündedir.
- Tehlikeli seçenek varsayılan olarak seçili gelmez; her alanın görünür bir etiketi vardır.
- Ayrıntı için [design/foundations.md](design/foundations.md) 5.2.

## 9. Bildirim

Bir olay tek yerde bildirilir: eylem gerektiriyorsa kalıcı şerit, yalnız bilgi veya başarıysa kendiliğinden kapanan
bildirim; aynı olay ikisinde birden görünmez (foundations 5.3).
