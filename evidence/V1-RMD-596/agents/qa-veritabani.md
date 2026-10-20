# qa-veritabani — V1-RMD-596 ham rapor özeti

105/105 kalem. Messaging testi PG 55432'de koşuldu: 42/42 exit 0 (ajan kaydı tmp/qa'da).

| No | Önem | Kanıt | Özet |
|---|---|---|---|
| 1 | Yüksek | Program.cs:286 yalnız idempotency_keys/device_sessions siler; OutboxStore.cs:265-267; DELETE outbox/inbox araması 0 | outbox_messages için saklama/temizlik yok; dispatched ve dead satırlar payload ile süresiz birikir; dead sayımı yavaşlar. |
| 2 | Orta | OutboxStore.cs:224-225; 003-outbox-messages.up.sql:17-19 | Alım sorgusu ORDER BY created_at, indeks bu sırayı karşılamıyor (EXPLAIN çalıştırılmadı). |
| 3 | Orta | OutboxStore.cs:178-180 | Kira geri alımında attempt_count artmaz; zehirli mesaj sonsuz döngü. |
| 4 | Orta | 002-inbox-messages.up.sql; kullanım yok | inbox_messages üretimde okunmuyor/yazılmıyor; ölü şema. |
| 5 | Orta (doküman sapması) | 012-btree-gist-ownership.down.sql:3-18 vs postgresql-extension-ownership.md:44 | Doküman DROP EXTENSION der, down hiç düşürmez; catalog.product_prices yoksa down hata verir. |
| 6 | Düşük | 002/003/033 up | IF NOT EXISTS tutarsız (001'de var). |
| 7 | Düşük | 002/003 up | CHECK/PK kısıtları adsız. |
| 8 | Düşük | OutboxStore.cs:246 | Enum.Parse("in_flight") gelecekte çöker. |
| 9 | Düşük | 001-idempotency-keys.up.sql:6-8; Program.cs:286 | expires_at indeksi yok; tek DELETE, partisiz. |
| 10 | not | OutboxStore.cs:61-110 | Bulgu değil: ilk EnqueueAsync aşırı yüklemesi kendi işleminde (çağıran riski). |

Temiz: SKIP LOCKED, kira nesli (033) ve testleri, down göçleri (FND-002/019), parametreleştirme, TIMESTAMPTZ/CHECK, hata sanitize, diğer 71 dosyada DB yüzeyi yok.
Doğrulanamayan: payload KVKK içeriği, plan etkisi, 033 down'unu kapsayan test yok, remaining-gaps taraması zaman aşımı.
