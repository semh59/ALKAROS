# ALKAROS Arayüz Yeniden Tasarımı — Faz 0: Temel Tasarım Dili

> **Durum:** Onaylandı (Semih, 2026-09-07) — Faz 0.1 (renk), 0.2 (tipografi), 0.3 (boşluk/dokunma hedefi), 0.4 (etkileşim ilkeleri). Faz 0 tamamlandı.
> **Kapsam:** Frontend'in sıfırdan yeniden yazılması kararının temeli. Bu doküman
> onaylandıktan sonra hiçbir modül ekranı bu kurallardan sapmaz; yeni bir
> ihtiyaç çıkarsa önce bu doküman güncellenir, sonra ekranlara yansır — tersi
> olmaz.
> **İlişki:** Kök dizindeki `DESIGN.md` (PDF kaynaklı V1 spesifikasyonu) bu
> dokümanın onayladığı iki kuralı zaten koymuştu ve burada da aynen korunuyor:
> 48px minimum dokunma hedefi ve "ham hata/İngilizce sızıntısı yok" kuralı
> (`docs/UI_STYLE_GUIDE.md`). Bu doküman DESIGN.md'nin görsel dilini (renk,
> tipografi) yeni marka kimliğiyle değiştirir; PDF'ten gelen davranışsal/işlevsel
> kararları (coursing, masa yaşam döngüsü vb.) değiştirmez.

## 0. Genel ilke: Backend akıllı, frontend aptal

Semih'in kararı (2026-09-07): frontend hiçbir iş kuralını tekrar üretmez,
yalnız backend'in söylediğini gösterir. Bu, projenin kendi tarihinde defalarca
doğrulanmış bir ilke — client tarafında iş mantığı tekrar yazan her modül
(`WaiterOfflineQueueEngine`, `OrderEntryEngine`, V1.1'in hiç bağlanmamış
`MenuRecipeAdminEngine`/`ProductionBatchEngine`/`InventoryPurchasingEngine`,
ve Host katmanında `DualScreenStore`'un `Order` aggregate'ini atlayan ham
SQL'i — bkz. `V1-RMD-120`) ya öldü ya da gerçek bir hataya dönüştü.

Somut kurallar:

1. **İstemci state tutmaz, yalnız gösterir.** Sipariş/bilet/onay durumu her
   zaman API'den gelen DTO'nun doğrudan yansımasıdır; istemci "şu an şu
   durumda olmalı" diye kendi kafasından hesaplamaz.
2. **Hangi aksiyon geçerli, backend söyler.** DTO'lar `canX: bool` gibi
   alanlar taşır; istemci koşulu kendi yeniden üretmez, yalnız gösterir/gizler.
3. **Jest/tıklama = tek bir API çağrısı; ekranın yeni hâli her zaman o
   çağrının cevabından (veya SignalR push'undan) gelir.** Ara adım/yerel
   simülasyon yok.
4. **İstemci doğrulaması yalnız UX içindir** (boş alan, format), asla
   otoriter değildir — otoriter doğrulama her zaman backend'de.

## 1. Renk sistemi

### 1.1 Marka token'ları (yeni logo paletinden)

| Token | Hex | Rol |
| --- | --- | --- |
| `--color-ink` | `#0B2135` | Koyu vurgu, üst bar, koyu zeminler (ör. mutfak KDS zemini) |
| `--color-brand` | `#1B4D7B` | İkincil marka — link, bilgi rozeti |
| `--color-accent` | `#00CFFF` | Yalnız **dolgu/çerçeve/odak halkası** — asla açık zeminde metin/ikon rengi |
| `--color-text` | `#222222` | Gövde metni |
| `--color-canvas` | `#F4F6F8` | Sayfa arka planı |
| `--color-surface` | `#FFFFFF` | Kart/panel zemini |
| `--color-border` | `#C8D0D7` | Ayraç/çizgi (mevcut `--ds-color-line` ile aynı — kasıtlı düşük kontrast, dekoratif ayraç) |

**`--color-accent` kullanım kuralı (WCAG hesabıyla doğrulandı):**

- `#00CFFF` dolgu + üzerine `#0B2135` (ink) metin/ikon → 8.86:1, AAA. **Doğru kullanım budur.**
- `#00CFFF` dolgu + üzerine beyaz metin/ikon → 1.85:1, **FAIL**. Kullanılmaz.
- `#00CFFF` açık zeminde (canvas veya surface) doğrudan metin/ikon rengi → 1.71-1.85:1, **FAIL**. Kullanılmaz.
- Koyu zeminde (`--color-ink` üzerinde) ikon/çizgi rengi olarak → serbest, yüksek kontrast.

### 1.2 İşlevsel (semantic) renkler — mevcut, kanıtlanmış değerler korunuyor

Yeni marka paletinde success/warning/danger/info karşılığı yok; icat etmek
yerine `tokens.css`'in halihazırda AAA seviyesinde doğrulanmış değerlerini
aynen taşıyoruz:

| Token | Hex | Beyaz üzerinde kontrast |
| --- | --- | --- |
| `--color-success` | `#176548` | 7.02:1 (AAA) |
| `--color-warning` | `#735400` | 7.01:1 (AAA) |
| `--color-danger` | `#9a2530` | 7.83:1 (AAA) |
| `--color-info` | `#0b5d8d` | 7.08:1 (AAA) — zaten `--color-brand` ailesiyle uyumlu |

Her birinin bir de "soft" (dolgu/rozet zemini) varyantı mevcut sistemde
zaten var (`--ds-color-success-soft` vb.) — aynen korunuyor.

### 1.3 Reddedilen ilk taslak (kayıt için)

İlk önerdiğim `#16A34A` (success), `#E8A33D` (warning), `#DC2626` (danger)
WCAG hesabıyla test edildi ve reddedildi: `#E8A33D` beyaz üzerinde 2.16:1
(FAIL), `#16A34A` yalnız 3.30:1 (büyük metin/UI bileşeni sınırı, gövde metni
için yetersiz). Mevcut sistemin değerleri daha sıkı; onlar kazandı.

## 2. Tipografi

**Inter** — tüm ağırlıklarıyla (Regular/Medium/SemiBold/Bold/ExtraBold),
tek font ailesi. Gerekçe:

- Küçük punto okunabilirliği POS ekranları için kanıtlanmış.
- Tabular rakam desteği (`font-variant-numeric: tabular-nums`) — fiyat/adet
  sütunlarında hizalama kaymaz.
- Başlıklarda ayrı bir display font yerine Inter'in kendi Bold/ExtraBold
  ağırlığı kullanılır — "en basit uygulama" ilkesiyle tek font ailesi.

## 3. Boşluk ve dokunma hedefleri

Mevcut `--ds-space-1..6` ölçeği (4/8/12/16/24/32px) korunuyor, yalnız
dokunma hedefi için yukarı genişletiliyor:

| Token | Değer | Kullanım |
| --- | --- | --- |
| `--target-min` | 48px | Mutlak minimum (DESIGN.md kararı, değişmedi) |
| `--target-primary` | 56-64px | Birincil/hızlı-dokunulan aksiyon (ör. mutfak "hazır" kaydırma alanı) |

## 4. Tema: tek ve açık

**Onaylandı (Semih, 2026-09-10): Mutfak KDS de dahil olmak üzere hiçbir ekran
koyu tema kullanmaz.** Faz 0'da açık bırakılan tek soru buydu ve kapandı.

Sonuçları:

- Tüm modüller §1'in aynı paletini kullanır; `--color-canvas` `#F4F6F8`
  zemin, `--color-surface` `#FFFFFF` kart, `--color-ink` `#0B2135` üst bar.
  Koyu zemin yalnız üst bar, kilit perdesi gibi *yüzey* rollerinde kalır —
  ekranın kendisi hiçbir yerde koyuya dönmez.
- `tokens.css`'in bugünkü `color-scheme: light` tanımı doğru ve yeterli;
  ikinci bir token seti, `prefers-color-scheme` dalı ya da tema anahtarı
  yazılmaz. Faz 0'ın notu bunun ayrı bir altyapı işi olacağını söylüyordu —
  o iş artık hiç yapılmayacak.
- Bir ekran koyu görünmek isterse cevap hayırdır; istisna gerekirse önce bu
  bölüm güncellenir.

Gerekçe: mutfak ekranı da servis alanının aydınlık ışığında okunur ve tek
tema, aynı bileşenlerin her modülde birebir aynı davranmasını sağlar —
"backend akıllı, frontend aptal" ilkesinin görsel karşılığı.

## 5. Etkileşim ilkeleri (Faz 0.4)

### 5.1 Jest sözlüğü

Tüm modüllerde aynı anlama gelir, ekran ekran yeniden icat edilmez.

| Jest | Anlamı | Renk/geri bildirim | Not |
| --- | --- | --- | --- |
| Sağa kaydır | İlerlet / onayla / olumlu aksiyon (hazır, onayla, kabul et) | `--color-success`, kısa "pin" sesi | Mutfak "hazır" burada |
| Sola kaydır | Dikkat çek / sorun bildir | `--color-warning` | **Asla doğrudan silme/iptal değil** — bir kişiye (garson/yönetici) bildirim gider, gerçek karar backend'in zaten sahip olduğu yetki akışında verilir |
| Tek dokunuş | Seç / aç / birincil aksiyon | — | |
| Uzun basma | Detay / ek seçenekler (context menu) | Hafif titreşim | Nadiren — "en basit" ilkesiyle çelişmesin |
| Sürükleme | Yeniden sırala / taşı (masa yerleşimi, sepet kalemi) | — | Yalnız gerçekten gerekli ekranlarda |

Not: "Sağa kaydır" ilk taslakta "iptal" olarak düşünülmüştü;
**"sorun bildir"**e çevrildi — kaydırma anlamını her modülde sabit tutmak
için, ve geri alınamaz bir aksiyonun (iptal) tek jestle, backend'in zaten
sahip olduğu yetki/gerekçe akışını (`bills.void` grant, sebep kodu)
atlayarak tetiklenmemesi için.

### 5.2 Onay kalıpları

- **Ucuz/geri alınabilir aksiyon** (hazır işaretle, sipariş onayla) → onay
  penceresi yok, tek dokunuş/jest uygular; ekranda birkaç saniye bir
  "Geri Al" (undo) seçeneği belirir.
- **Pahalı/geri alınamaz aksiyon** (void, iptal, silme) → her zaman
  backend'in zaten sahip olduğu yetki/gerekçe akışına girer; istemci bunu
  asla tek jestle atlamaz, en az bir açık onay adımı olur.
- Kural: "Bu aksiyon ücretsiz mi geri alınabiliyor?" sorusu onay penceresi
  olup olmayacağını belirler — rastgele "emin misiniz?" spamı yok.

### 5.3 Bildirim türleri

| Tür | Ne zaman | Süre |
| --- | --- | --- |
| Toast (kendiliğinden kapanır) | Başarılı işlem, undo seçeneği | 3-5 sn |
| Banner (kalıcı, aksiyon gerektirir) | Onay bekleyen QR siparişi, çözülmemiş sorun bildirimi | Aksiyon alınana kadar |
| Push (uygulama kapalı/arka planda) | Mutfak "hazır" ve misafir siparişi — SignalR (açıkken) + Web Push (kapalıyken) ikilisi | — |

Web Push V1-WTR-011'de gerçekten yazıldı (RFC 8291 + RFC 8292, ek paket
olmadan). İki kanal birlikte kullanılır, biri seçilmez: SignalR anında ama
yalnız açık uygulamaya ulaşır, push kilitli ekranı da geçer. Aynı bildirimi
iki kez göstermemek için ikisi de aynı `tag`'i taşır.

### 5.4 Hata gösterimi

Mevcut kural değişmiyor: ham `error.message`/HTTP kodu asla ekrana
basılmaz (`docs/UI_STYLE_GUIDE.md`), her hata önceden tanımlı Türkçe
sözlükten geçer. Ağ hatası → "Sunucuya ulaşılamadı, tekrar deneniyor"
tarzı, teknik detay yok.

## Sonraki adım

Faz 0 tamamlandı. Şimdi modül modül sırayla: Garson → Mutfak → Kasa/POS →
Masa Yönetimi → Müşteri (QR) → Yönetim/Arka ofis.
