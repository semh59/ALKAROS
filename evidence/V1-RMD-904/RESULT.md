# V1-RMD-904 sonucu

V1-RMD-891..905 sonrası tam regresyon yeşil: Host.Tests 60/60 proje, PosTerminal vitest 1134/1134 (104 dosya) ve tsc temiz, StaticApps vitest 444/444 (41 dosya).

## Kırmızılar (ilk koşuda 4 proje, hepsi önceden vardı, bu zincirin değişikliklerine bağlı değil)
- CashSession.Tests (1): yönetim ekranının sayı aralığı listesine billing.adjustment_total_cap eklenmemişti (V1-RMD-885'ten). Teste eklendi; ClientLimitLockTests 9/9 (clientlimit-fix.log).
- Composition.Tests (1): Host ikinci bir IPrePolicyGate (BillAdjustmentTotalGate, V1-RMD-885) kaydettiği için testin "tek kayıt" varsayımı bozuldu. Test birden çok kaydı da çözüyor; ProductionExperienceCompositionTests 9/9 (composition-fix.log).
- NfcOrdering.Tests (1) ve QrOrdering.Tests (2): V1-RMD-800'ün yarım porsiyon göçü bu iki test projesine eklenmemişti, katalog uç noktası 503 dönüyordu. csproj'lara eklendi; 29/29 ve 35/35 (NfcOrdering-fix.log, QrOrdering-fix.log).
- Ayrıştırma kanıtı: bu zincir models.ts, katalog göçü ve test csproj'larına dokunmadı (902 BillingSplitApplication.cs'e dokundu ama ikinci kapı kaydı 885'ten); kırmızılar 885 ve 800'ün kapanışında kendi projeleri dışındaki test projeleri çalıştırılmadığı için fark edilmemişti.

## Toplam
Host.Tests ilk koşu: 2572 geçti, 5 başarısız (4 projede; hepsi aşağıda düzeltildi ve ilgili sınıflar yeşil).

## Kanıt
host/summary.txt (60 satır, ilk koşu), host/*.log, posterminal-vitest.log, tsc.log, staticapps-vitest.log.
