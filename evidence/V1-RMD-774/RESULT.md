# V1-RMD-774 sonuç

## Kırmızı (düzeltmeden önce)
- Sunucu: `red-host/` — 4 yeni DualScreenStoreTests başarısız (zorunlu grup, seçenek kaydı, yabancı seçenek, en çok seçim).
- Domain: `red-domain/` — ChangeQuantityKeepsTheOptionOnEveryPortion başarısız.
- Ön yüz: `red-frontend.log` — yeni 3 testten 2'si başarısız.

## Yeşil
- Domain: `green-domain6/` 159/159; mutasyon `mutation-domain/` (bölmeyi kaldırma) 4 test kırmızı, geri alındı (cmp).
- Sunucu (gerçek Postgres): `green-host4/` DualScreenStoreTests 29/29; `green-host5/` TableDraft + Orders.Comp + diğer DualScreen 115/115.
- Ön yüz: `typecheck2.log` exit 0; `green-frontend-all3.log` 86 dosya / 808 test; lint (`lint2.log`) 2 hata var, ikisi de bu işin dışındaki dosyalarda (TerminalsPanel.tsx, CustomerDisplay.tsx), görev dosyalarında hata yok.
- Plan audit ve consistency audit: `plan-audit.log`, `consistency-audit.log`; görev kapsamı: `task-scope.log` (exit 0).

## Bu oturumda bulunan ve giderilen kusurlar
1. `OrderItem.ChangeQuantity` seçenek adedini önce oran (1/3) sonra çarpma ile hesaplıyordu; 3 -> 1 adette 0,999... çıkıyordu. Çarpıp bölme sırasına çevrildi (`green-domain3` başarısız, `green-domain4` yeşil).
2. `PostgresOrderRepository.UpdateItemAsync` seçenek adedini veritabanına yazmıyordu; orantılanan adet kaydedilmiyordu (`green-host3` başarısız, `green-host4` yeşil). Plan iki kez daha genişletildi (964b08a2).
3. İsteğe bağlı tek seçimli grup radyo olduğu için seçim geri alınamıyordu; onay kutusuna çevrildi, test eklendi.

## Mutasyon
- Sunucu: `mutation-host/` — grup denetimi kaldırılınca 2 test kırmızı (ARequiredOptionGroup..., MoreSelectionsThanTheGroupAllowsAreRefused).
- Ön yüz: `mutation-frontend.log` — pencere koşulu kaldırılınca 2 test kırmızı. Kaynaklar geri alındı (güncel diff'te koşul ve denetim çağrısı mevcut).

## Açık riskler / sınırlar
- Seçici açıkken sipariş yarışı (activeOrder bayat) ve hatada seçimlerin kaybolması: bağımsız incelemede belirtildi, bu görevde kapsam dışı bırakıldı.
- Çok seçimli grupta üst sınır aşılınca onay düğmesi nedensiz pasif kalıyor (yalnız görsel ipucu eksik).
- Lint: görev dışı 2 hata önceden mevcut.
