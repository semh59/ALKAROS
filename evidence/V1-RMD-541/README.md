# V1-RMD-541 - Arayüz etkileşim kaydı

- İstemci (`features/interaction-telemetry`, `main.tsx`'te kurulur): tıklama (denetim öğeleri), form gönderimi ve rota değişimi kaydedilir. Her kayıt: sabit anahtar, tür, süre (dokunuştan sonraki çizime), 50 ms üstü uzun görev sayısı, düzen kayması ve sonuç (sayfa hatasıyla çakıştıysa `error`).
- Anahtar sırası: `data-telemetry-id` -> kontrol metninin `strings.ts` sözlük yolu (`t:...`; metin yalnız tarayıcıda aranır, gönderilmez) -> `auto:<etiket>@<rota şablonu>`. Metin, erişilebilir ad ve girilen değer anahtara girmez; sunucu da yalnız katalog biçimli anahtar kabul eder (en çok 64 karakter, e-posta/serbest metin biçimi reddedilir).
- Kuyruk: kendi depolama anahtarı ve 20.000 karakter sınırı (sipariş kuyruğuna dokunmaz), en çok 200 kayıt, 50'lik gruplar, gönderim başarısız olursa saklanır.
- Sunucu (`POST /api/v1/telemetry/interactions`, kasiyer çerezi): grup başına en çok 50 kayıt, terminal başına dakikada 120, en çok 500 farklı anahtar (fazlası `other`), 32 KB gövde sınırı; zaman sunucu saatindendir (istemci yaş gönderir). `observability.raw_ui_interactions` (migration 191) `raw_` önekli ve UNLOGGED; saklama kataloğunda 30 gün; `deploy/docker/backup.sh` `--no-unlogged-table-data` ile ham veriyi yedek dışında bırakır. Ayrıca V1-RMD-543 `ui_interaction` ölçümünü besler.

Kanıt: `test-runs.log` (sunucu 15/15 gerçek Postgres testi, PosTerminal 590/590, tsc 0, Manifest + DualScreenHostTests 23/23), `mutations.log` (sunucu 9, istemci 15 mutant; hepsi öldü). Yedek testi gerçek `backup.sh` + `pg_dump` + `pg_restore --list` ile: ham tablonun tanımı var, verisi yok; normal tablonun verisi var.
`button-keys.txt`: 304 düğme sitesi, 50 dosya, hiçbirinde `data-telemetry-id` yok; statik tarama bu sayının artmasını engeller. Sıfırlama V1-RMD-567'de.

Doğrulanamadı / sınırlar:
- Tıklama ek yükü gerçek kasa cihazında ölçülmedi (V1-RMD-556).
- Geçerli kasiyer oturumuyla uç noktanın 204 dönmesi HTTP ile denenmedi (oturumsuz 401 ve gövde sınırı denendi; kayıt mantığı doğrudan sınıfla test edildi).
- Çoğu düğme şimdilik `auto:` anahtarına düşer (rota düzeyi); metni `strings.ts`'de olanlar sözlük anahtarı alır; aynı metin birden çok yerde geçiyorsa ilk sözlük girdisi kullanılır.
- Rol yalnız `cashier` yazılır (uç nokta kasiyer çerezini kullanır); garson/mutfak düz JS uygulamaları V1-RMD-553.
- Tarayıcıda uzun görev ve düzen kayması gözlemcisi yalnız desteklenen tarayıcılarda çalışır; jsdom'da gözlemci yok.
