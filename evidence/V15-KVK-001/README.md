# V15-KVK-001 - kabul kanıtı

- Yeni modül `Privacy.RetentionExecution` (migration 170, `privacy` şeması): sürümlü saklama politikası (sürüm 1 = onaylı veri
  envanteri: personel hesabı pasif olduktan 1 yıl, sipariş ve rezervasyon notları 5 yıl, müşteri ve tedarikçi 10 yıl),
  yasal bekletme, çalıştırma kaydı ve vadesi gelen her kayıt için değişmez bir iş kalemi. PO kararı (2026-09-30): müşteri
  süresi son hesap hareketi ya da fatura tarihinden, tedarikçi süresi son sipariş tarihinden başlar; açık bakiyesi ya da
  bekleyen anonimleştirme talebi olan müşteri seçilmez.
- Prova ve yürütme aynı seçimi kullanır: prova hiçbir şey yazmaz, yürütme aynı kayıtları çalıştırma kaydına ve iş kalemlerine
  yazar (`tests.log`, 11 test; `ThePlanWritesNothingAndTheExecutedRunSelectsExactlyTheSameRecords`).
- Yasal bekletme: bekletilen kayıt plan ve yürütmeden çıkar, veritabanı bekletilen kayıt için iş kalemi yazmayı ve tamamlamayı
  reddeder. Tekrarlanan çalışma stabildir: ikinci ve daha geç tarihli çalışma iş kalemi eklemez, yalnız boş çalışma kaydı
  bırakır; eşzamanlı dört çalışma her iş kalemini bir kez yazar.
- Bekletme dışı denetim: politika sürümleri, kurallar, çalıştırmalar ve kalemleri değişmez; iş kalemi yalnız Pending → Done.
  Eksik politika (her sınıf için süre yok) işlem sonunda reddedilir. Bekletmeyi kaldırma kaydı korunur, düzenlenemez.
- Mevcut `kvkk-retention` komutu artık bu politikaya ve seçime bağlıdır (iki ayrı motor yok): personel, sipariş/kalem notu ve
  rezervasyon gerekçesi eskisi gibi silinir ve iş kalemi aynı işlemde Done olur; müşteri ve tedarikçi yalnız bekleyen iş kalemi
  olarak kuyruğa girer (alan düzeyinde anonimleştirme V15-KVK-002). Çıktıya `customers`, `suppliers` ve `policy` eklendi.
  Gerçek komut denemesi: `gercek-deneme.log` (prova sıfır kayıt yazar, uygulama bekletmeli siparişi atlar, ikinci uygulama
  sıfır, bekletme kalkınca kalan sipariş anonimleşir; borçlu müşteri seçilmez).
- Kontrol kaldırılınca test kırmızı: bekletme dışlaması çıkarılınca `ALegalHoldKeepsARecordOutOfBothThePlanAndTheRun`
  (`red-without-hold-exclusion.log`). Migration 170 ileri/geri/ileri temiz (`migration-up-down.log`).
- Modül sınırı, API kuralları, bileşim/manifest ve komut testleri ile tutarlılık ve proje manifesti denetimi yeşil.
- Kesinti sonrası devam: yürütme iş kalemlerini yazıp işlemi bitirir, temizleme ayrı işlemdir; arada kesilirse bekleyen kalemler
  bir sonraki `--apply` ile tamamlanır (`AnApplyFinishesWorkItemsThatWereQueuedButNeverScrubbed`, `tests.log`).
- Envanterin dokuz satırı: Müşteri bilgisi, kullanıcı bilgisi (personel hesabı), sipariş notları ve tedarikçi bilgisi bu görevde
  (rezervasyon gerekçesiyle birlikte; alan düzeyi uygulama müşteri ve tedarikçi için V15-KVK-002). Sağlayıcı ham verisi
  V15-SEC-003. Denetim kayıtları eklemeli olduğundan V15-KVK-002. Mali fiş ve fatura verisi yasal olarak saklanır (dokunulmaz).
  Cihaz verisi ("cihaz kullanımdan kalkınca sil") için veritabanında kullanımdan kaldırma durumu yoktur; kimsenin kapsamında
  değil, tahmine dayalı sınıf eklenmedi (açık boşluk).
- Devir notu: müşteri ya da tedarikçi kalemi bir kez Pending olunca yeniden seçilmez ya da iptal edilmez; V15-KVK-002 alan
  düzeyinde silmeden önce uygunluğu (bakiye, yakın hareket, bekleyen talep) kendi işleminde yeniden denetlemelidir.
- Kapsam notu: "tüm mağazalar" için mağaza başına kontrol noktası ve devam ettirme V15-KVK-002'dedir; denetim kayıtlarının 10
  yıllık anonimleştirmesi eklemeli (append-only) tablo yüzünden onun kapsamında kalır.
