# Dalga 1 — davranış bazlı ayrıştırma

- Aktif program: V1-RMD-739; üretim kodu değiştirilmedi.
- 1.376 kaynak kaydı ve 300 yönlendirme paketi önceki aşamada doğrulandı; bunlar kesin hata/görev sayısı değildir.
- Ödeme dilimi: 15 kaynak kaydı -> 12 davranış/kural taslağı. PAY-012 ana sayıya eklenmeyen yeni statik adaydır.
- Kaynak satırları, mevcut production/test yolları ve hashler kontrol edildi; kabul matrisi yazıldı. Host testi çalıştırılmadı, bu kayıtlar kapanmadı. Üretim SQL seçimi gerçek PostgreSQL üzerinde geçici tablolarda sınandı (exit 0).
- Somut kalan örnekler: tahsissiz tam indirimli hesabın sweep seçimine girmemesi; Paid sonrası sipariş tamamlama hatasının yeniden seçilmemesi; manuel kart çözümünde Pending claim yaşam döngüsü.
- PO/provider kapıları: mali Paid tanımı; iade slip kapsamı; manuel iade görevler ayrılığı; allowance bekleme/iptal kuralı; tutar üst sınırı. Teknik karar diye varsayılmayacak.
- Stok: 21; yetki/gizlilik:18 kaynak kaydının tam kart/kod ayrıştırması bağımsız iki çalışmada sürüyor. Sonuç gelmeden tamamlandı denmez.
- Sonraki: stok/yetki taslaklarını exact kaynak ve kapsamla doğrula; üç dilimin davranışlarını ve ortak dosya bağımlılıklarını eşleştir; bağımsız denetim sonrası exact Task ID planlaması.
- Tüm 1.376 kaydın kök neden ayrıştırması henüz bitmedi. Bu dalga başlangıç dilimidir.

## Yeni çalıştırılmış seçim kanıtı
- 201 daha yeni kapanamayan hesap, daha eski kapanabilir hesabı ilk ve ikinci turda dışarıda bırakıyor; DESC LIMIT 200 genel adil ilerleme sağlamıyor.
- Tahsissiz hesap ve Paid durumundaki hesap seçimden dışlanıyor. Bunlar kaynakta görülen kurtarma boşluklarını destekler; tam Host hata enjeksiyonu henüz yok.
- Komut: `python -B evidence/V1-RMD-739/classification/wave1/probe_sweeper_selection.py`; exit 0; log: `sweeper-selection-probe.log`. Kalıcı tablo/veri değiştirilmedi, temp tablo ve ROLLBACK kullanıldı.

## Bağımsız inceleme sonrası (2 statik denetçi)
- F0031 (V11-INV-008) tam kapanış değil: delta=0 dalı korumasız (PhysicalCountService.cs:66-77). Ana sayım 16 tam / 29 kısmi oldu; validate exit 0.
- Gereksiz karar engelleri kaldırıldı: GYZ-008/010/012/016/019, PAY-004 teknik; GYZ-011 açık; GYZ-024 bilinen sınır (görev yok).
- Birleşmeler: STK 006+007, 012+013, 024+025, 037+038, 022+023, 001→002; PAY-G09+G12; IAM-008 karar+belge. 70 → 62 aday.
- Sıra: stok rebuild düzeltmeleri erişilebilirlik kararından sonra; aynı dosyalı ödeme grupları seri.
- Eksik yazma yüzeyi eklendi: CardSettlementModule.cs, order.json, stockApi.ts. Stok fake repository dosyaları görev yazılırken eklenecek.
- Probe denetçi tarafından ayrıca koşuldu: exit 0.
