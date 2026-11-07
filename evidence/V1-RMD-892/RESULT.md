# V1-RMD-892 sonucu

Yönetici onayına düşen (202) ve reddedilen (403) ikram/indirim istekleri artık denetim izine yazılıyor: `bill.comp.pending`, `bill.discount.pending`, `bill.comp.denied`, `bill.discount.denied` (hesap, isteği yapan, gerekçe, hibe kimliği, tutar, hesaplama türü).

## Tekilleştirme
Kasiyer ekranı bekleyen isteği aynı anahtarla tekrar göndererek yokluyor. Olay kimliği hibe kimliği + olay adından SHA-256 ile türetiliyor ve olay zamanı hibenin kendi zamanı (`RequestedAt`, reddedilmişte `ResolvedAt`). `audit.audit_events` birincil anahtarı (id, occurred_at) olduğundan (yıllık bölümleme, migration 173) ikinci yazım 23505 verir ve yutulur. Yalnız kimlik sabit olsaydı bölümlenmiş tabloda tekilleştirme çalışmazdı: ilk deneme tam bunu gösterdi (green-1.log: 5 yoklama = 5 kayıt). Bu yöntem eşzamanlı yoklamada ve ilk yazımın yarıda kalıp tekrarlandığında da doğru; ek tablo/sorgu yok.

## Davranış
- Politika reddi veya yöneticinin reddi: reddetme olayı, kasiyerin reddi öğrendiği ilk istekte (ya da politika reddinde istek anında) yazılır. Aynı hibe için bir kez.
- Onaylanan istek: `pending` kaydı kalır, uygulamada `applied` kaydı eklenir (mevcut).

## Kanıt
- Kırmızı red-1.log (5/5 kırmızı). Yeşil: final-audit.log (7/7: tur 1 incelemesi sonrası eşzamanlı yoklama ve anında-red testleri eklendi); bitişik BillingSplit/BillAdjustment testleri final-split.log (85/85).
- Mutasyon (mutations.txt): rastgele kimlik, olay zamanı "şimdi", 23505 yutulmaz, reddi "pending" adlandırma, ikram/indirim adı yer değiştirmiş, isteği yapan boş: tur 1: 6/6, tur 2 (23505 filtresi hesaplanan şemaya daraltıldı): şema filtresi ve rastgele kimlik, tur 3 (kayıt içeriği assert'leri eklendi): tutar sıfırlama, olay zamanı, red adı, isteği yapan: hepsi öldü; her biri cp+cmp ile geri alındı (restored-identical), sonra yeniden derlenip yeşil doğrulandı.

## Açık sınır
- Yöneticinin bekleyen hibeyi onaylama/reddetme kararının kendisi (`AuthorizationDecisionStore`) hâlâ denetim olayı yazmıyor; ret ancak kasiyer yeniden sorgulayınca kayda geçer, onay ise uygulamadaki `applied` olayındaki onaylayan alanında görünür. Karar anının doğrudan denetimi ayrı bir iştir (plan Out of scope).
