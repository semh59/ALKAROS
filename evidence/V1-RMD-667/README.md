# V1-RMD-667 — PIN kilidi zinciri

Kapanan bulgular (V1-RMD-597): B001, B003, B004, B024, B032.

| Bulgu | Düzeltme | Kanıt testi |
|---|---|---|
| B001 | PIN kilitliyken `/auth/unlock` sunucu oturumunu iptal eder, çerezi siler; yenileme ya da elle API çağrısı artık 401 alır | `WrongPinsThatLockTheDeviceEndTheServerSessionSoARefreshCannotWalkAround` |
| B003 | `/auth/pin` yanlış mevcut parolayı giriş sayacına yazar; kilitli hesapta reddeder | `WrongPasswordsOnThePinEndpointCountTowardsTheAccountLock` |
| B004 | Yönetici kilit açma PIN sayaç ve kilidini de sıfırlar; kullanıcı arama PIN kilidini "kilitli" gösterir | `ForceUnlockAlsoClearsThePinLockAndItsCounter`, `LookupShowsAPinOnlyLockAsLockedAndForceUnlockClearsIt` |
| B024 | PIN 4–12 ASCII rakam; 1111/1234/4321 reddedilir; giriş ve oturum yanıtı `pinSet` döner, istemci kilidi buradan kurar; bağlantı yok/5xx/429'da "PIN hatalı" denmez, yazılan PIN silinmez | `PinsMustBeFourToTwelveAsciiDigitsAndNotPredictable` (9 durum), `LoginAndSessionReportWhetherAPinIsSet` |
| B032 | Kilit ekranı veri yüklemesinden önce kurulur | tarayıcıda denenmedi (aşağıda) |

## Koşu (test-*.log)
Authentication 61/61, DualScreen host (DualScreenAuthorizationHttpTests) 19/19, SecurityAdministration 31/31; hepsi 0 başarısız, 0 atlanan.

## Mutasyon (mutations.log, mutate.mjs)
8 mutant, 8'i öldü, her turda dosya orijinale döndürüldü ve cmp ile doğrulandı: oturum iptalini kaldırma, parola sayacını kaldırma, kilit kontrolünü kaldırma, ForceUnlock'ta PIN sütunları, aramada PIN kilidi, uzunluk üst sınırı 12→13, ardışık artan PIN kuralı, pinSet.

## Doğrulanamayan / bilinçli açık
- İstemci davranışı (hata mesajı ayrımı, kilidin yüklemeden önce kurulması, yeni PIN yardım metni) tarayıcıda ve cihazda denenmedi; WaiterPwa E2E 07-kiosk-lock koşulmadı (tarayıcı/canlı yığın gerektirir). Sunucu sözleşmesi testlidir.
- Bilinen yan etki: yanlış PIN yazan biri garsonun cihaz oturumunu kapatabilir (garson şifreyle yeniden girer). Kabul edildi: kilidi sunucuda zorlamanın bedeli.
- B019 (doğru PIN ile oturum ömrü yenilemesi) bu görevde yapılmadı, V1-RMD-672'ye taşındı.
- Bu görevde gelen davranış değişikliği: `SetPinAsync` artık `DateTimeOffset now` alır.
