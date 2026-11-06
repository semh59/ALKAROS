# V1-RMD-853 sonuç

Kasa ve Garson istemcisinin davranışı yalnız kaynak metninde dize aranarak sınanıyordu; bu testler jsdom davranış testlerine çevrildi ve silindi. Üretim koduna dokunulmadı (yeni testler mevcut kodda yeşil çıktı: kusur bulunmadı, eksik olan test kapsamıydı). Kırmızı kanıtı mutasyonlardır.

## Silinen her iddianın karşılığı
| Silinen dize iddiası | Davranış testi |
|---|---|
| park/recall işlevleri vardır (`test_cashier_frontend.py`) | `cashier-ticket-and-catalog.test.js`: boş sepete onaysız geri yükleme; dolu sepette onay sorulur, vazgeçilince sepet korunur, kabulde değişir |
| `orders/table/` dizesi (kırık; kodda hiç yoktu) | karşılığı yok: iddia yanlıştı, kaldırıldı |
| dispatch çift tıklama koruması, çevrimiçi rozeti, bozuk localStorage | zaten davranış testiyle var: `cashier-app.test.js` (çift gönderim, bağlantı rozeti, bozuk bekleyen fiş listesi) |
| manifest ve işaretleme sözleşmesi, "sahte tahsilat yok" | `cashier static contract` testleri (manifest alanları, kontrol kimlikleri, tahsilat iddiası yok) |
| `renderStockBadge(prod.remainingCount)` ve CSS (`test_cashier_remaining_count.py`) | kalan adet 4 -> `is-low`, 5 -> değil, takipsiz üründe rozet yok |
| `Number(p.unitPrice ?? 0)`, `categoryCode` (`test_cashier_catalog_field_mapping.py`) | gerçek alan adlarıyla fiyat 180,00 ve kategori sekmeleri; `cashier-app.test.js` fixture'ı gerçek olmayan `currentPrice`/`categoryId`'den `unitPrice`/`categoryCode`'a düzeltildi |
| Türkçe küçük harf kuralı (`test_cashier_catalog_search.py`) | zaten `cashier-search-fold.test.js` (Isparta/İstanbul/Iğdır); boş sonuç iletisi yeni testle |
| katalog hatasında sahte ürün yok, oturum adı sabit değil (F0060) | katalog 500: kart yok, ileti, gönder düğmesi kapalı; oturum adı sunucudan, "Zeynep" yok |
| Garson menü yeniden deneme ve boş menü (`test_waiter_catalog_retry.py`) | `waiter-app.test.js`: ilk katalog 500 -> "Menü alınamadı." -> yeniden dene -> ürünler; sunucu ürün göndermezse "Menüde ürün yok." ve yeniden dene düğmesi yok |
| sahte ikram / sahte tahsilat yok (`isComplimentary`, `completeCashSale`, `changeDue`, `changeModal`) | kasa sipariş ekranında ikram eylemi yok (davranış), işaretlemede `changeModal` ve tahsilat iddiası yok |
| kalan-adet rozeti CSS'i | `cashier static contract`: `.product-stock` ve `.product-stock.is-low` kuralları |

## Kanıt
- `first-run.log`/`second-run.log`: yeni testler yeşil. `full-static.log`: StaticApps 34 dosya, 383 test geçti; `pytest-clients.log`: tests/Clients Python 33/33 (F0079'daki "6 failed" artık yok).
- Mutasyon `mutations.log` (cp+cmp ile geri alındı): geri yükleme onayı atlanırsa, kalan-adet eşiği, fiyat alanı, katalog yokken gönderme, kategori alanı, sabit kasiyer adı, boş arama iletisi, garson yeniden deneme düğmesi, Türkçe küçük harf kuralı ve garson boş menü iletisi bozulursa: onunda da bir test düşer.

## Açık not
- F0355'in anlattığı E2E senaryosu (`20-parked-ticket-recall-confirm`) depoda yok; park/recall davranışı artık jsdom testiyle korunuyor, tarayıcı E2E eklenmedi.
- Bilinen zayıf noktalar (inceleme): fiyat testi yalnız `180,00` biçimine bakar; park sayacı iki fişli senaryoyla sınanmadı; oturum alınamayınca işaretlemedeki varsayılan "Kasiyer" etiketi ayrıca sınanmadı.
- Silinen iki iddia ayrıca karşılanmadı: kasa kodunda "Tükendi" durumu ve `completeCashSale`/`changeDue` yokluğu (bugün kodda yok; tahsilat iddiası ve ikram eylemi yokluğu işaretleme/DOM testiyle korunur).
