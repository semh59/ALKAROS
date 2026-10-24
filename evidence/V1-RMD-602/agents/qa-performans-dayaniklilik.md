# qa-performans-dayaniklilik - V1-RMD-602 ham rapor (özet biçim)

Kontrol: 22/142 kalem (120 kalem okunmadı: PosTerminal/Cashier/PWA arayüzleri, testler, E2E, CashierHandoffStore, ShiftSummaryStore, tüketici sınıflar, Order.cs, DualScreenStore.Orders). Ölçüm yok, kanıt düzeyi hepsinde Kod okuması.

| No | Önem | Kanıt | Sorun |
| --- | --- | --- | --- |
| P1 | Orta | SentItemVoidStore.cs:149-155 (yorum 23-29) | Gönderilmiş kalem iptali üç ayrı commit: sipariş+stok+denetim, mutfak fişi kalemi, hesap satırı. Sonraki adım düşerse kalem iptal ama mutfak hazırlar / hesapta tam fiyat kalır; yeniden deneme StaleOrderRowVersion 409 alır, onarım yolu yok. Sınıf yorumu bunu kabul edilmiş ödünleşim olarak belgeliyor. |
| P2 | Orta | OrderManagementEndpoints.cs:822-838 | void-sent ucunda `order-item.void-sent` denetim olayı işlemden sonra ayrı yazılıyor; çökmede kaybolur (asıl denetim SentItemVoidStore.cs:145 korunur). |
| P3 | Orta | SentItemVoidStore.cs:81-82; OrderManagementEndpoints.cs:1160 | void-sent isteğinde OperationId yok; yanıtı kaybolan yeniden deneme 409 CONCURRENCY_CONFLICT alır (Semih senaryosunun yarısı garsona "çakışma" görünür). |
| P4 | Orta | QrOrderExpiryHostedService.cs:109-127 | Süre dolumu döngüsünde yalnız üç istisna tipi yutulur; kalıcı hata veren ilk sipariş (ORDER BY updated_at LIMIT 50) arkasındakilerin süresinin dolmasını engeller, masalar Reserved kalır. |
| P5 | Orta | OrderStockConsumptionService.cs:140-198, 334-358 | Kalem başına ayrı sorgular havuzdan ayrı bağlantılarla ve stok advisory kilidi tutulurken; kilit süresi kalemle doğrusal uzar. |
| P6 | Orta (çıkarım) | SubmitOrderHandler.cs:85-86,129; TableDraftService.cs:283; SentItemVoidStore.cs:183; HostComposition.cs:362; DualScreenApplication.cs:149 | İşlem açıkken ikinci bağlantı alınır (PostgresOrderRepository.cs:52 işlem içi aşırı yüklemesi kullanılmıyor); havuz boyutu/komut zaman aşımı ayarsız, yoğunlukta havuz kilitlenebilir (ölçülmedi). |
| P7 | Düşük | NfcOrderingStore.cs:281-287 | Duyuru commit sonrası hata fırlatırsa müşteri 500 alır, sipariş oluşmuştur. |
| P8 | Düşük | OrderManagementEndpoints.cs:542-545 | Mutfağa gönderim commit sonrası müşteri ekranı hub çağrısı hata verirse istek 500 döner. |
| P9 | Düşük | OrderReadStore.cs:97-126 | Bekleyen sipariş listesinde LIMIT/sayfalama yok. |
| P10 | Düşük | OrderManagementEndpoints.cs:1188-1189 | OperationCanceled/Timeout eşlenmemiş; istemci kopması 500 Error logu. |
| P11 | Düşük (doğrulanamadı) | SubmitOrderRequestHash.cs:40; offline-queue.js:220 | ChangedBy hash'te; vardiya devrinde aynı anahtar farklı kullanıcıyla 409 IDEMPOTENCY_KEY_REUSED alıp kuyrukta takılabilir. |

Temiz: ağ kopması yeniden denemesi (istemci tek payload.id, table-draft yeniden oynatma + ux_orders_table_submission, submit-draft operationId, hash'te ExpectedRowVersion/SubmittedAt yok); eşzamanlı aynı anahtarlı istekler (CAS); stok+fiş+durum+anahtar aynı işlemde; denetim aynı işlemde; gönderilmiş kalem iptalinde stok iadesi aynı işlemde; NFC çift sipariş ve yaş kısıtı sunucuda; indeksler; istemci kuyruğu (üstel geri çekilme, kalıcı); hosted service döngüsü.

Doğrulanamayan: havuz tükenmesi ölçümü, WaiterNotificationDispatch hata davranışı, CashierQueueAnnouncer, Order.FireRound/CancelItem iç mantığı, yinelenmiş iptalde CanReverseAsync, çalışan işi/hız/öğrenme değerlendirmesi.
