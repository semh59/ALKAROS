# V1-RMD-653 kanıtı

- Test: `test-runs.log` (Host Observability testleri, temiz derleme, 0 uyarı); ilk yeşil koşu `green.log`.
- `AChangeWhoseAuditRecordCannotBeWrittenIsNotMadeAndRemovingAMissingBudgetLeavesNoRecord`: veritabanında denetim olayı reddedilince bütçe eklenmez/değişmez/kaldırılmaz; olmayan bütçenin kaldırılması 404 döner ve denetim satırı üretmez.
- Mutasyon: `mutations.log` 3/3 yakalandı (değişiklik denetimden önce; kaldırmada varlık kontrolü yok; kaldırmada değişiklik denetimden önce), dosya geri alındı (aynı).
- Kırmızı kanıt: eski sıra (değişiklik sonra denetim) mutasyonla geri getirildi ve test başarısız oldu.
- Kalan risk: bütçe ayrı veri kaynağında olduğu için denetim yazıldıktan sonra değişiklik başarısız olursa denetimde yapılmamış bir deneme izi kalır (kabul edilen yön).
