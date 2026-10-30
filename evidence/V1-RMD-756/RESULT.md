# V1-RMD-756 sonuç

Bulgu F0068: ağ hatasından sonra değişen indirim, bahşiş veya kasa hareketi eski idempotency anahtarıyla gidiyordu.

## Kanıt
- Kırmızı (`red/`): yama öncesi kaynak, yeni testler: 3 failed, 6 passed.
- Yeşil (`green/`): 05 + 09 + 18 spec, gerçek Chromium + Host + PostgreSQL: 15 passed, exit 0. Kaynak hash'leri b6d06690 ile aynı.
- Mutasyon (`mutations/`): 6 mutant, 6 öldürüldü, kaynaklar geri yüklendi.
- Semih senaryosu (spec 18 ve 05): indirim 10 -> 20 sonrası kalan ₺80; bahşiş 5 -> 10 sonrası kalan ₺110; kasa 500 açılış, 50 -> 70 giriş, sayım 570, fark ₺0.
- Kapsam: `scope-vs-3d0ab5a7.json` valid=true. `scope-vs-inprogress-baseline-6efd0151.json` valid=false: 6 bulgu, hepsi 3d0ab5a7 (V1-RMD-739 yabancı yama arşivi) commit'inden; 756'ya ait değil. Uygulama commit'i b6d06690, taban 3d0ab5a7.
- Bağımsız inceleme: deleg_228a8f33, 2 PASS, engelleyici bulgu yok.

## Saklanan başarısız koşular
- `failed-attempt-patch-not-applied/`: yama uygulanmadan koşan ilk "yeşil" deneme (3 failed). Label düzeltildi.
- `green-09-only/`: yalnız spec 09 ara koşusu (6 passed).

## Sınırlar
- Testler ağ hatasını `route.abort` ile üretir; istek sunucuya ulaşıp yanıt kaybolan durum sunucu kaydıyla doğrulanmaz.
- Not, sebep ve tür değişikliği ayrı test edilmez; imza bu alanları kapsar.
- Tahsilat (`submitTender`) anahtarı kapsam dışı.
