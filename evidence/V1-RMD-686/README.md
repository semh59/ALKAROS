# V1-RMD-686 — Ayar geçmişi, toplu okuma, test boşlukları, doküman

## Yapılan
- Geçmiş: ikincil sıralama (`changed_at DESC, setting_history_id DESC`); 5000'den uzun geçmiş artık hata vermez (en yeni 5000); uç nokta `limit` (varsayılan 100, en çok 500) ve `offset` alır.
- GarsonFeatureToggles.GetAllAsync: dokuz ayrı sorgu yerine tek sorgu; sonuç tek tek okumayla aynı (eksik/pasif/okunamaz = açık).
- Yeni testler (TypedSettings test projesi): GarsonFeatureToggles 5, QrOrderExpiry 3, geçmiş sıralaması ve uzun geçmiş 2.
- settings-ownership.md koda göre "Known settings as built" tablosuyla düzeltildi.

## Kanıt
- typed-tests.log: 54/54 yeşil.
- mutations.log: 8/8 mutant KILLED. Gereksiz yeniden okuma tespit edilip koddan silindi.

## Yapılmayan / doğrulanamadı
- Tekrarlı DI kayıtları (7 dosyada TryAdd*) silinmedi: dosyalar bu görevin yüzeyi dışında, kayıtlar zararsız (ilk kayıt kazanır). Ayrı görev gerekir.
- İki yeni test projesi kapı tarafından yüzey dışı sayıldı; testler mevcut TypedSettings projesine taşındı.
- `limit`/`offset` için HTTP testi yok (SettingsManagementHttpTests yüzey dışı).
- Tarayıcıda elle deneme yapılmadı.
