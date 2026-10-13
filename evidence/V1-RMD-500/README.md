# V1-RMD-500 - Reçete yönetim ekranı

## Kurallar

- Yönetim ekranına `inventory.manage` yetkisiyle açılan yeni "Tarifler" bölümü eklendi: tarif oluşturma, sürüm listesi, ilk sürüm (verim miktarı ve birimiyle) veya yeni taslak sürüm, malzeme ekleme/çıkarma, sürümü etkinleştirme ve kilitleme, özel birim dönüşümü ekleme, ürün–tarif eşlemesi atama/görme/kaldırma.
- Düğmeler sunucu kuralını yansıtır: yalnız taslak sürüm etkinleştirilir ve malzemesi düzenlenir; yalnız etkin ve kilitsiz sürüm kilitlenir. Sayı alanları geçersizse sunucuya gidilmez.
- Sürüm durumları Türkçe etiket tablosundan geçer (bilinmeyen değer "Bilinmiyor"). Hata olarak yalnız sunucunun Türkçe sözleşmeli metni veya sabit "İşlem tamamlanamadı." gösterilir. Her POST bir idempotency anahtarı taşır.
- Sunucu uçları değişmedi; ekran `RecipeManagementEndpoints.cs` ve `RecipeCatalogMappingEndpoints.cs` yollarını çağırır (`api.test.ts` her yolu, yöntemi ve gövdeyi doğrular).

## Kanıt

- `tests-client.log`: tüm istemci 64 dosya / 480 test geçti (yeni: 13 test — bölüm 9, yol sözleşmesi 4). `typecheck.log`, `oxlint.log`, `build.log`: exit 0.
- `mutation.log`: 8 mutant, hepsi en az bir testi kırdı (etkinleştirme/kilit görünürlüğü, malzeme formu, geçersiz sayı, sürüm numarası, eşleme yolları, ham hata metni); iki mutant ilk turda hayatta kaldı, test sıkılaştırılıp yeniden koşuldu.
- Gerçek-Host denemesi ayrıca yapılmadı: sunucu tarafı değişmedi ve bu uçlar kendi Host testleriyle kapsanır; istemci–sunucu sözleşmesi yol testiyle sabitlendi.

## Açık kalan

- Reçete arşivleme/silme ve malzeme miktarı düzenleme ekranı yok; sunucuda da böyle bir uç bulunmuyor (taslakta çıkar + yeniden ekle kullanılır).
- Sürümlerin ürün eşlemesi tarif düzeyindedir (sunucu sözleşmesi), sürüm seçmez.
