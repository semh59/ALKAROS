# Mutfak: Tüm Gün Görünümü + Performans Raporu — rakip araştırması ve karar kaydı

- **Tarih**: 2026-09-14
- **İstek**: Semih — "Rakiplerimiz üstünde bir iş yapalım. Derin düşün ve
  araştır." (Kitchen redesign backlog'unun "redesign kapsamı" tier'ındaki
  son iki büyük parça: Tüm Gün Görünümü ve mutfak performans raporu.)
- **Yöntem**: gerçek vendor dokümantasyonu (Toast, Lightspeed, Oracle
  Simphony, Fresh KDS, Foodics) + gerçek bir Toast ürün-güncelleme kaydında
  bulunan gerçek bir bug + genel restoran-iletişim istatistiği. Vendor
  pazarlama sayfaları değil, destek dokümantasyonu ve ürün güncelleme
  günlükleri tercih edildi.

## 1. Rakiplerin "Tüm Gün Görünümü" (All Day View) ne yapıyor

**Toast KDS All Day View** ([support.toasttab.com](https://support.toasttab.com/en/article/KDS-All-Day-1493055871075)):
aktif biletlerdeki AYNI ürünleri tek bir toplu kutuda birleştirip sayar
(ör. 18 hamburger → tek kutu, "18"). İki grup modu var: yalnız ürün
bazlı (modifier'lardan bağımsız toplama) veya ürün+modifier alt
gruplaması (üstte toplam, altta modifier bazlı kırılım). Amaç: yüksek
hacimli/dar menülü mutfaklarda elle sayma ihtiyacını ortadan kaldırmak.

**Gerçek bulunan bir bug** (Toast'ın kendi ürün güncelleme kaydı): held
(bekletilen — ALKAROS'ta `KitchenState.Held`, V1-WTR-025'in kurs/fire
mekanizması) kalemler bir süre All Day View'da SAYILARA dahil oluyordu,
bu da mutfak personelini "hazır sanılan ama aslında bekletilen" kalemler
yüzünden yanıltıyordu. Toast bunu held kalemleri toplamdan hariç tutarak
düzeltti.

## 2. Rakiplerin "mutfak performans raporu" ne ölçüyor

| Sistem | Rapor | Ölçtüğü |
| --- | --- | --- |
| Toast | Tickets by Fulfillment Time | istasyon bazlı, zaman aralığına göre bilet sayısı |
| Toast | Tickets by Hour | saatlik satış + bilet sayısı + ortalama tamamlama süresi |
| Toast | Ticket Details | bilet bazlı, dışa aktarılabilir (CSV) |
| Lightspeed | KDS Statistics | ürün bazlı ortalama hazırlama süresi, en hızlı/en yavaş ürünler, saat/gün bazlı ısı haritası |
| Oracle Simphony (kurumsal kıyas noktası) | KDS Consolidated Menu Item Prep Times | **tahmini vs gerçek** hazırlama süresi karşılaştırması, gün bölümüne göre |
| Oracle Simphony | Guest Experience Summary/Detail | hazırlama+gönderim+masa süresi, gelir merkezi/gün bölümü/**çalışan** bazlı |
| Oracle Simphony | Order Aging Report | check/misafir sayısı + **recall (yeniden çağırma) sayısı**, gün bölümüne göre |
| Fresh KDS | Speed-of-service kategorileri | hızlı/orta/yavaş dağılımı (yapılandırılabilir eşiklerle) |
| Foodics | genel analitik | hazırlama süreleri, yoğun saatler, personel verimliliği |

## 3. Gerçek boşluklar/dersler (vendor dokümantasyonundan çıkarıldı)

- **Ortalama yanıltıcı olabilir**: Fresh KDS'in kendi dokümanı
  "averages can both hide extreme values" diyor — bir istasyonun
  ortalaması iyi görünse de birkaç aşırı-yavaş bilet gizlenebilir.
  Çözüm: ortalama YANINDA medyan/dağılım göstermek.
- **Held kalem/All Day View karışıklığı** (yukarıda, gerçek Toast bug'ı)
  — ALKAROS'un kendi `Held` durumu zaten var, bu hatayı en baştan
  önlememiz gerekiyor.
- **Segmentasyon eksikliği**: bazı sistemler servis tipine (masa/paket)
  göre ayrıştırma sunmuyor — ALKAROS'ta artık gerçek `TableId`
  (V1-KIT-012) var, bu segmentasyonu bedavaya yapabiliriz.
- **Kök neden değil, yalnız semptom**: hiçbir rakip dokümanı "NEDEN
  yavaş" sorusuna cevap vermiyor, yalnız "NE KADAR yavaş" gösteriyor.

## 4. Gerçek personel şikayeti / iletişim bulgusu

Restoran çalışanlarının **%62**'si FOH/BOH iletişimsizliğinin düzenli
yaşandığını, **%72.25**'i verimsiz iletişimin iki takım arasındaki
çatışmanın ana nedeni olduğunu söylüyor (Checkmate'in derlediği sektör
istatistiği). Bu, "mutfak performansı" kavramının yalnız hız değil,
**doğruluk/düzeltme oranı** boyutunu da içermesi gerektiğini destekliyor.

**İlgili ama bu göreve dahil edilmeyen bir bulgu**: rakiplerin çoğu
(Fresh, ve dolaylı olarak diğerleri) "recall" diye bir kavram taşıyor —
TAMAMLANMIŞ bir bileti yüksek öncelikle yeniden ateşleyip mutfağa geri
göndermek (yanlış/eksik çıkan bir yemek fark edildiğinde). Bu, ALKAROS'un
`V1-KIT-009` "undo"sundan (yalnız 10sn'lik kısa pencere, kaza-düzeltme
amaçlı) FARKLI bir yetenek — ayrı bir gerçek boşluk, bu görevin kapsamı
dışında, ileride ayrı bir Task ID olarak değerlendirilebilir.

## 5. ALKAROS'un elindeki, rakiplerin çoğunun ödeme duvarının arkasında tuttuğu avantajlar (mevcut kod tabanında zaten var, yeniden icat edilmeyecek)

- `KitchenTicket.TargetPrepMinutes` zaten her bilette var — ama
  **düzeltme (bu araştırmanın kendi dürüstlük kontrolü)**: bugün bu
  yalnız sabit bir global varsayılan (`DefaultTargetPrepMinutes = 15`,
  `KitchenTicket.cs:16`), veritabanında HİÇ saklanmıyor
  (`PostgresKitchenTicketRepository`'de karşılığı yok) — Oracle'ın
  ürün-bazlı gerçek tahminiyle aynı değil. Yine de "her bilet aynı
  15dk hedefe göre ne kadar aştı" karşılaştırması dürüst ve anlamlı bir
  ilk metrik (sıfır ek veri modeliyle, bugün zaten hesaplanabilir);
  ürün-bazlı gerçek tahmin ayrı, daha büyük bir fast-follow.
- `V1-KIT-009`'un undo'su — **ikinci düzeltme (dürüstlük kontrolü)**:
  bir geri alma bugün yalnız satırı YERİNDE günceller
  (`KitchenTicketItem.Undo()`, `ReadyAt`/`ServedAt` temizlenir), ayrı bir
  olay/log satırı YAZILMIYOR — `_audit` (`IAuditEventStore`) Kitchen
  tarafından hiç yazılmıyor, yalnız okunuyor
  (`KitchenOperationsStore.cs`'de `AppendAsync` çağrısı yok). Yani
  "kaç kalem geri alındı" bugün GERİYE DÖNÜK hesaplanamaz — veri zaten
  var değil, icat edilmiş bir metrik olurdu. Gerçek bir "düzeltme oranı"
  raporu için önce undo anında bir audit event yazılması gerekir — bu,
  performans raporunun ilk sürümüne DAHİL EDİLMEDİ, ayrı bir fast-follow
  olarak not edildi (aşağıya bkz.).
- `V1-KIT-012`'nin gerçek `TableId`/`TableNumber`'ı — servis tipine göre
  segmentasyonu bedavaya sağlıyor.
- Self-hosted/yerel ağ mimarisi (`V1-KIT-011`) — rapor ekranı internet
  kesintisinde de çalışır; cloud-öncelikli rakiplerin raporlama
  ekranları genelde internet gerektirir.

## 6. Kararlar (bu araştırmadan çıkan, Task ID'lere dökülecek)

1. **Tüm Gün Görünümü** (`V1-KDS-008`) — Expo'nun yanına ikinci bir görünüm
   modu (sekme/toggle), zaten yüklü `data.tickets`'tan İSTEMCİ TARAFINDA
   hesaplanır (yeni bir backend ucu GEREKMEZ — veri zaten var). Ürün adına
   göre toplar, **`Held` durumundaki kalemleri toplamdan hariç tutar**
   (Toast'ın gerçek hatasından ders). İlk sürüm yalnız ürün bazlı toplama
   yapar (modifier alt gruplaması ayrı bir fast-follow, bu görevde yok).
2. **Mutfak performans raporu** (`V1-KIT-014` backend + `V1-KDS-009`
   frontend) — yeni bir Kitchen HTTP ucu: istasyon bazlı ortalama VE
   medyan tamamlama süresi (`created_at`→`ready_at`), sabit 15dk hedefe
   göre hedef-aşım oranı (bugünkü gerçek veri, ürün-bazlı tahmin değil —
   dürüstçe böyle etiketlenir), saatlik bilet hacmi. Zaman aralığı
   filtresi (bugün/son 7 gün). Veri kaynağı: mevcut
   `kitchen.kitchen_tickets`/`kitchen_ticket_items` tablolarının zaten
   var olan `created_at`/`ready_at`/`served_at`/`cancelled_at`
   kolonları — yeni bir event-tracking şeması icat edilmiyor.
   **"Düzeltme oranı" (undo/correction rate) bu ilk sürüme DAHİL
   DEĞİL** — bölüm 5'te açıklandığı gibi, bugün undo anında hiçbir
   audit event yazılmıyor, bu veri henüz yok; ayrı bir fast-follow
   (önce `KitchenOperationsStore.UndoItemAsync`'e bir audit-event yazma
   adımı eklenmesi gerekir, sonra rapor bunu okuyabilir).

## 7. Kaynaklar

- [Toast KDS All Day View](https://support.toasttab.com/en/article/KDS-All-Day-1493055871075)
- [Toast Kitchen Operations Reports Overview](https://support.toasttab.com/en/article/Kitchen-Reports-Overview)
- [Lightspeed KDS Statistics](https://k-series-support.lightspeedhq.com/hc/en-us/articles/4403156122651-KDS-Statistics)
- [Oracle Simphony KDS Reports](https://docs.oracle.com/en/industries/food-beverage/simphony/19.4/kdscu/c_kds_reports.htm)
- [Fresh KDS — game-changing order report features](https://www.fresh.technology/blog/are-you-missing-out-on-game-changing-order-report-features-for-your-kitchen)
- [Fresh KDS — Recall feature](https://www.fresh.technology/kds-features/recall)
- [Checkmate — reducing order errors, FOH/BOH miscommunication stats](https://www.itsacheckmate.com/blog/reducing-order-errors-in-restaurants---a-complete-guide)
