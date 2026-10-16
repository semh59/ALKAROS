# V1-RMD-567 - PosTerminal düğmelerine telemetri anahtarı

49 kaynak dosyadaki 301 düğme sitesine `data-telemetry-id="b.<özellik>.<dosya>.<sıra>"` eklendi (yalnız öznitelik; davranış, metin ve görünüm değişmedi). Tasarım sistemi içindeki 3 `<button>` bileşen tanımıdır, anahtar çağıran yerden gelir. V1-RMD-541 statik taraması artık anahtarsız düğme sitesi sınırını 0 tutar, anahtarların katalog biçimli ve benzersiz olduğunu doğrular.

Kanıt: `test-runs.log` (PosTerminal 591/591, tsc 0), `mutations.log` (öznitelik silme, çift anahtar, geçersiz biçim: üçü de taramada yakalanır).

Sınırlar:
- Anahtar dosya adı ve sıradan türetildi; aynı dosyada düğme eklenip çıkarılırsa sıralı numaralar kayar (geçmiş veri farklı düğmeye bağlanabilir). Anlamlı adlar sonraki bir düzenlemeyle verilebilir.
- `.map` ile üretilen düğmeler tek anahtarı paylaşır (öğe başına ayrım yok; bu istenen davranış).
- İngilizce sızıntı taraması (UI_STYLE_GUIDE §4) anahtarlarda `catalog`/`pending`/`draft` gibi dosya adı kaynaklı 10 eşleşme gösterir; bunlar öznitelik değeridir, ekranda görünmez.
- Düz JS kasa ve garson uygulamaları bu görevin dışındadır (V1-RMD-553).
