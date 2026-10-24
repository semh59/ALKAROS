# V1-RMD-717 - Masa komutları için idempotency anahtarı

Kapanan V1-RMD-601 bulgusu: B031. Olay tekilleştirme ve ölü olay yeniden oynatma için yeni kod yazılmadı: tüketiciler olay kimlik listesiyle tekrar teslimde değişiklik yapmaz (`ARedeliveredTransferEventLeavesTheNextGuestsOrderAlone` ve benzerleri) ve ölü mesajlar V1-RMD-288 ekranından listelenip yeniden kuyruğa alınır.

## Değişiklikler
- `TableIdempotencyFilter`: rezervasyon oluşturma, transfer, birleştirme ve ayırma uçlarında `X-Idempotency-Key` varsa ilk 200 yanıtı `idempotency_keys` tablosuna (mevcut, yeni migration yok) yazar; aynı anahtar aynı istekle gelirse ilk yanıtı döner, başka istekle gelirse `IDEMPOTENCY_KEY_REUSED` (409) verir. Anahtar terminale ve oturumdaki kullanıcıya bağlıdır, 24 saat geçerlidir. Aynı anahtarlı eşzamanlı iki istek PostgreSQL advisory kilidiyle sıraya girer. Başlık yoksa uç eskisi gibi çalışır.
- PosTerminal: açılan her işlem penceresi bir anahtar üretir; zaman aşımından sonra "Onayla"ya tekrar basmak aynı anahtarı yollar.
- İdempotency allowlist'ten 4 uç çıktı; doküman satırı güncellendi.

## Test kanıtı (gerçek PostgreSQL)
Host Tables idempotency 7 test (tekrar, anahtarsız 409, farklı istekle yeniden kullanım, ayırma tekrarı, eşzamanlı birleştirme, kapsam, süresi dolmuş anahtar); vitest tables+routes 166; ApiConventions. Günlükler `logs/`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
8 mutantın 8'i KILLED, kaynak geri yüklendi ve doğrulandı. I3 (kullanıcı kapsamı) ve I5 (süre dolumu) ilk koşuda SURVIVED; test sıkılaştırıldı / eklendi, yeniden koşuda KILLED.

## Doğrulanamayan / açık kalan
- Garson uygulaması (WaiterPwa) anahtar göndermiyor; başlık isteğe bağlı olduğundan çalışmaya devam eder ama orada tekrar koruması yok.
- Durum değişikliği, bölge/masa düzenleme, rezervasyon claim/cancel/expire uçları hâlâ anahtarsız (allowlist'te).
- Yalnız 200 yanıtlar saklanır; hata yanıtı tekrarında komut yeniden denenir.
