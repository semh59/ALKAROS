# V1-RMD-648 kanıtı

- Testler: `test-runs.log` (Void 5, Comp 16, TableDraft 89; temiz derleme, 0 uyarı).
- Kalem iptali/ikram: uç noktadaki ikinci kayıt kaldırıldı; testler artık atomik `Order.ItemVoided` / `Order.ItemComplimentary` olayının tek olduğunu doğrular (eski testler ikinci kaydı doğruluyordu).
- Geri alma: `ARecallWhoseAuditRecordCannotBeWrittenIsNotKept` (denetim hata verirse masa bağlanmaz, hesap iptal edilmez) ve geri alma testi tek denetim satırını doğrular.
- Mutasyon: `mutations.log` 2/2 yakalandı (denetim commit'ten sonra; olay adı değişimi), dosya geri alındı (aynı). Kalem iptali/ikram için ikinci kaydın geri gelmesi `Assert.Single` ile yakalanır (betikle mutasyon yapılmadı).
- Kalan risk: gönderilmiş kalem iptali (`order-item.void-sent` ve mağazanın kendi `Order.SentItemVoided` kaydı) tek işlemde değil; ayrı görev.
