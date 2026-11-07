# V1-RMD-885 sonucu

Hesap başına toplam ikram+indirim sınırı eklendi. Her istek eskiden yalnız kendi rolünün tek-istek limitine bakıyordu; aynı hesapta 5 x 50 TL ikram parça parça otomatik onaylanabiliyordu.

## Davranış değişikliği (ürün kararı, Semih 2026-11-07)
- Bir hesapta `bills.comp` ve `bills.discount` hibelerinin (onaylanmış, `granted`) toplamı artı yeni istek, sınırı aşıyorsa istek otomatik onay yerine yönetici onayına düşer (bekleyen istek, 202).
- Sınır ayarı `billing.adjustment_total_cap` (tam TL, 0..100.000, yönetim ayarlar ekranından değişir). Varsayılan 150 TL = `PersonalCompBudgetPolicy.DailyCap`; yeni sihirli sayı yok. 0 = her ikram ve indirim yönetici onayı ister.
- Sınıra tam ulaşmak serbest; aşan ilk istek bekler. Bekleyen istek toplamı doldurmaz; yönetici onayı verilen sayılır.
- Etkilenmeyenler: izne doğrudan sahip roller (yönetici/müdür) kapıdan geçmez, sınırlanmaz; tek-istek limitleri ve politika tablosu aynı.
- Eşzamanlı istekler: hesap anahtarlı danışma kilidi (istek sahibi kilidinden önce alınır); 4 eşzamanlı 50 TL istek tam 2 onay + 2 bekleyen verir.

## Açık sınırlar (bilinçli)
- Garsonun kalem ikramı (`OrderItem` konulu hibe, kişisel bütçe tavanı) hesap toplamına katılmaz.
- Geri alınan (undo) ikram/indirimler toplamda sayılmaya devam eder (tutucu yön).

## Kanıt
- Kapı: 9/9 (green-gate.log), yetki projesi tamamı 105/105 (final-gate.log); HTTP 5/5 (final-host.log); ayarlar 37/37 (green-settings.log); vitest 26/26 ve tsc 0 (green-vitest.log, green-tsc.log).
- Mutasyon (mutations.txt): 9 mutant, hepsi öldü (`>=`, indirim kapsam dışı, önceki toplam yok, her konu türü, her izin, her durum toplanıyor, hesap kilidi yok, kapı kaydı yok, ayar yok sayılıyor). İlk kilit mutantı derleme hatasıydı (sayılmadı), geçerli mutantla yeniden koşuldu. Her mutasyon cp+cmp ile geri alındı, sonra yeniden derlenip yeşil doğrulandı.
- Plan yüzeyi düzeltildi: kullanılmayan `IdentityModule.cs` ve `OrderManagementEndpoints.cs` çıkarıldı; kapı `BillingSplitApplication.cs` içinde kayıtlı.

## İnceleme sonrası notlar
- Doğrudan `bills.comp`/`bills.discount` iznine sahip rol (supervisor ve üstü) hibe isteği açmaz; verdiği tutar hibe toplamına girmez. Aynı hesapta yönetici doğrudan büyük indirim verdikten sonra kasiyer yine sınıra kadar onaysız ikram verebilir. Plandaki "izne sahip roller etkilenmez" kararının sonucudur; ürün kararı gerekirse toplam `bill_adjustments` üzerinden hesaplanmalıdır.
- Uygulanamayan (ör. hesap durumu hatası) ama `granted` yazılmış hibe toplamda kalır; sonuç güvenli yönde (gereksiz yönetici onayı).
- Sınır bileşeni, ayar servisi bazı host kayıtlarında scoped olduğu için ayarı her okumada kapsam açarak çözer (singleton kapıda scoped bağımlılık hatasını önler).
- Sınır aşıldığında istek doğrudan yönetici onayına düşer; etkin yetki devri (delegation) ve kişisel ikram bütçesi çözücüleri atlanır (davranışsal sıkılaştırma kapısıyla aynı mekanizma). Yani devir sahibi de hesap sınırını aşamaz.
- Yüzdeli indirim yolu da sınıra katılır (PercentageDiscountsOfOneBillAddUpToTheCapToo).
