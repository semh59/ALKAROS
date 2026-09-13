# V1-RMD-153 - Garson istemcisinde denetimin bulduğu kusurlar

- Task ID: V1-RMD-153
- Status: Done
- Assignee: Claude Opus 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-10'da beş bağımsız ajanla yapılan Garson ekranı denetiminin istemci
tarafındaki bulgularını kapatır. Sunucu tarafındaki bulgular ayrı görevlerde.

En ağırı bir güvenlik kusuru: `escapeHtml` tırnak kaçırmıyor ve dört yerde
insan yazımı veri çift tırnaklı HTML niteliğinin içine konuyor. İkincisi
sessiz veri kaybı: kuyruğa alınan sipariş, kuyruğa girmesine yol açan
durumda bir daha hiç denenmiyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-153-waiter-client-audit-defects.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js,
    src/Clients/WaiterPwa/wwwroot/waiter-app.css (V1-WTR-010 sahipliğinde).

## In scope

1. **Nitelik içine XSS (Critical).** `escapeHtml` metin düğümü üzerinden
   çalıştığı için yalnız `&`, `<`, `>` kaçırıyor; `"` olduğu gibi geçiyor.
   Ürün adı, masa numarası, kalem adı ve kalem notu çift tırnaklı
   niteliklere yazılıyor. Menüye tırnak içeren ad giren yönetici ya da
   QR'dan tırnaklı not yazan misafir (*Turu tekrarla* o notu kopyalıyor)
   garsonun oturumunda kod çalıştırabiliyor.
2. **Kuyruğa alınan sipariş bir daha denenmiyor (High).** `flushQueue`
   yalnız `online` olayında ve açılışta çağrılıyor. Oysa kuyruğa girmenin
   asıl yolu sunucunun 5xx dönmesi; o sırada ağ ayakta olduğu için `online`
   hiç tetiklenmiyor ve sipariş `localStorage`'da kalıyor.
3. **Masa değiştirince gönderilmemiş tur siliniyor (High).** Ekranda "tur
   duruyor" yazan bir uyarı çıkıyor, hemen ardından tur siliniyor.
4. **"Geri al" yanlış masaya ekleyebiliyor (High).** Geri alma kapanışı
   hangi masaya ait olduğunu tutmuyor; toast 5 saniye yaşadığı için bu süre
   içinde masa değiştirmek ya da turu göndermek mümkün.
5. **Adisyon toplamı istemcide yeniden hesaplanıyor (High).** `OrderDto`
   `TotalAmount` taşıyor, istemci onu hiç okumuyor; masa kartı ise sunucunun
   değerini kullanıyor. İkram/indirim sonrası iki ekran çelişiyor.
6. **48px altındaki iki dokunma hedefi (Medium).** Adet basamağı 40px,
   not alanları 40px — `foundations.md` §3 mutlak asgari 48px diyor.

## Out of scope

- Sunucu tarafı bulguları (iptal edilen kalemin stoğunun iade edilmemesi,
  gönderilmiş kalemin yönetici onayı olmadan iptal edilebilmesi, void/comp'un
  `serving_user_id`'yi NULL yapması): kendi görevleri.
- Denetimin Low sınıfı bulguları ve ölü CSS temizliği.
- Erişilebilirlik açıkları (sayfa odak tuzağı, Escape, kilidin klavyeyi
  engellememesi): ayrı bir görev, ayrı test yüzeyi.

## Dependencies

- V1-WTR-010

## Acceptance evidence

Altısı da tarayıcıda, kusurun kendisi üretilerek denendi (yerel HTTP sunucusu

- scratchpad'de kalan geçici API stub'ı; stub menüye saldırıyı taşıyan bir
ürün adı ve satır toplamıyla çelişen bir sunucu toplamı koydu).

1. **XSS kapandı.** Menüde `Kola" onmouseover="window.__XSS=1` adlı ürünle:
   `aria-label` niteliği `&quot;` taşıyor, düğmede `onmouseover` niteliği
   **oluşmuyor** (`hasAttribute('onmouseover') === false`), gerçek bir
   `mouseover` olayı gönderildiğinde `window.__XSS` set **edilmiyor**, ve ad
   ekranda düz metin olarak görünüyor. `escapeHtml` artık `"` ve `'` de
   kaçırıyor.
2. **Kuyruk kendi kendine boşalıyor.** Sunucu ayakta ama 503 dönerken tur
   kuyruğa alındı (şerit: "1 bekleyen"), sonra sunucu düzeltildi ve
   **hiçbir olay gönderilmeden, hiçbir yere tıklanmadan** beklendi: tur 37
   saniye sonra gitti, şerit boşaldı. Eski kodda `online` olayı hiç
   tetiklenmediği için orada kalırdı. Geri çekilme 15 sn'den başlayıp 5 dk'ya
   kadar ikiye katlanıyor, başarıda sıfırlanıyor; 429 artık kalıcı hata
   sayılmıyor; üst üste iki boşaltma engellendi.
3. **Tur masa değiştirince kayboluyor.** M-01'e iki kalem girildi, M-02'ye
   bakıldı, geri dönüldü: sayaç 2 → 2. Ekrandaki mesaj da artık doğru
   ("turu saklandı"), önce "duruyor" deyip hemen silen hâli gitti.
4. **"Geri al" kapanışı** artık ait olduğu masayı ve tur kuşağını
   (`draftEpoch`) tutuyor; masa değiştiyse ya da tur gönderildiyse geri
   alma çalışmıyor, Türkçe bir uyarı veriyor.
5. **Adisyon toplamı sunucudan.** Sunucu `totalAmount: 200` derken satırlar
   500 tuttuğunda (ikram edilmiş kalem) adisyon **₺200,00** gösteriyor —
   masa kartıyla artık çelişmiyor.
6. **Dokunma hedefleri.** Açık ekranda görünen bütün etkileşimli öğeler
   tarandı: 48px altında **hiçbiri** kalmadı (adet basamağı ve not alanları
   40px'ti). Yatay taşma da yok (`scrollWidth == clientWidth`).

- `node --check waiter-app.js`: temiz.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: bu görevden yeni
  ihlal yok (kalan tek ihlal `InventoryAdjustmentService.cs:96`, diff'te
  değil).

## Handoff

- None
