# qa-performans-dayaniklilik — V1-RMD-596 ham rapor özeti

Envanter 105/105; yaklaşık 50 dosya tam okundu. Messaging testi koşuldu: 42/42 exit 0. Performans ölçümü yok.

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| P1 | Yüksek | RetryPolicy.cs:11 MaxAttempts=3; OutboxStore.cs:28 baseDelay 5 sn | Profilsiz olay türleri ~15 sn içinde ölü mektup olur; 8 iç tüketici bu yoldan; DB kesintisi >15 sn olayı kalıcı öldürür, geri dönüş yalnız elle requeue. |
| P2 | Yüksek | OutboxDispatcherHostedService.cs:~70,~97; 003-outbox-messages.up.sql:17-19 | status='dead' sayımı kısmi indeks dışı → tablo taraması; dolu döngüde, watcher 30 sn'de, admin listesinde; dispatched satırlar hiç silinmez (V15-OBS-003 planda, kodda yok). Ölçülmedi. |
| P3 | Orta | OutboxStore.cs:~131 | Sonuç yazımı iptal belirteci kullanır; kapanışta başarılı teslimat dispatched yazılamaz → 5 dk sonra çift teslim. |
| P4 | Orta | OutboxStore.cs:~125-136,~220; OutboxStoreTests.cs:240 | Bir mesajın kira kaybı döngüyü bitirir; kalan ≤49 mesaj 5 dk in_flight. |
| P5 | Orta | OutboxStore.cs:~160-170 | Lease dolumunda attempt_count artmaz; süreci çökerten zehirli mesaj ölü mektuba düşmez. (çalıştırılmadı) |
| P6 | Orta | OutboxStore.cs:~118; RetryPolicy.cs:~146-152 | İstisna ne last_error'a ne loga yazılır; kök neden bulunamaz. |
| P7 | Orta | OutboxStore.cs:~103-138 | Tüketici çağrısında zaman aşımı yok; takılan tüketici tüm akışı durdurur. |
| P8 | Orta | OutboxDispatcherHostedService.cs:~86-96 | DB kesintisinde sabit 1 sn bekleme, geri çekilme/jitter yok, log seli. |
| P9 | Orta | UnitConverter.cs:~30-48; UnitConversionLoaderHostedService.cs:~61-69 (dilim dışı) | Özel dönüşümler yalnız bellekte; açılışta DB hatasında bir daha yüklenmez; koli→adet sessizce 1:1. |
| P10 | Düşük | OutboxComposition.cs:~21 | Partinin 50 mesajı tek scope'ta; durum taşıma riski (doğrulanmadı). |
| P11 | Düşük | OutboxStore.cs:~156-185 | Boşta döngüde her sn iki sorgu. |
| P12 | Düşük | OutboxStore.cs:~193; 003 indeks | Alım ORDER BY created_at, indeks kapsamıyor. |
| P13 | Düşük | InMemorySecretProvider.cs:~11 | Thread-safe değil; üretimde kullanılmıyor. |
| P14 | Düşük | AesGcmEnvelopeCipher.cs:~84 | Her işlemde anahtar çözümü/tahsis. |

Temiz: kira nesil yarışı, SKIP LOCKED, geri çekilme formülü, singleton yaşam süreleri, hata yutma yok, transaksiyonel enqueue, SQL allowlist, testler.
Doğrulanamayan: P2/P12 gerçek hacim, P5 çökme senaryosu, P10 etkisi.
