# V1-RMD-790 - Kasa kapanışı (backend): ayarlı tolerans, fark açıklaması, açık hesap uyarısı, oturum sahibi, kilitli kapanış

- Task ID: V1-RMD-790
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 3. satırı ve 2. satırın mutabakat kısmı (`7-birlesik-dogrulama.md`, koddan doğrulandı) ile 1.020 kayıtlı doğrulama programından F0091 (kapanışta `ActualCash` istek gövdesinden alınıyor, kayıtlı sayım aranmıyor), F0353 (`ValidateCanStartCount` `cashierUserId`'yi kullanmıyor; tasarım belgesi eski), F0445 (tolerans 50 TL koda gömülü), F0420 (kapanışta eşzamanlı iki isteğin yarışı), F0926 (sayım kaydı oturum satırına bağlı değil), F0408 (mutabakat yetkisi belge-kod uyumsuzluğu) kapanır. PO kararları (2026-11-03) aynen uygulanır:

1. Açık hesap veya sonucu belirsiz ödeme: kasa kapanırken işletmede ödenmemiş hesap (`billing.bills` durumu `Paid`/`Cancelled` dışı) veya sonucu belirsiz ödeme (`payments.payments` durumu `Pending`, `Unknown`, `ReconciliationRequired`; kodun başka yerlerindeki "ödenmemiş" tanımı ile aynı) varsa kasiyer sayıları görür ve bilinçli onayla (`acknowledgeUnsettled`) kapatır; engel yoktur. Sayılar işletme geneli okunur (hesaplar ve ödemeler terminale bağlı değildir; uydurma terminal süzgeci kurulmaz, tüm kasalar kilitlenmez). Onay yoksa 409 `CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED`; onayda sunucunun o anda saydığı iki sayı ve onay bayrağı kapanışla aynı işlemde `audit` olayı (`cash.session.closed`) olarak yazılır.
2. Kapanış fark toleransı yönetici ayarıdır: `cash.close_variance_tolerance` (WholeNumber, TL, 0-500, varsayılan 50). SERT ÜST TAVAN 500 TL ayar doğrulayıcısında (`SettingValidator`) durur; ayar değişikliği zaten 212 numaralı tetikleyiciyle audit'e düşer (yeni anahtar için test eklenir). Politika artık varsayılan tolerans taşımaz; çağıran verir, negatif tolerans reddedilir. Fark SIFIRDAN FARKLIYSA (tolerans içinde olsa da) açıklama zorunludur (kırpılmış 1-500 karakter, 400 `CASH_DIFFERENCE_EXPLANATION_REQUIRED`); kural alanda ve veritabanında (`NOT VALID` denetim kısıtı) durur.
3. Oturum sahibi: sayıma başlama, sayım kaydı ve kapanış, oturumu açan kasiyer ya da `cash.session.override` izinli kullanıcı tarafından yapılır (yetki denetimi yalnız sahibi olmayan kullanıcıda çağrılır; sahibi olan normal kapanışta sahte ret kaydı oluşmaz). Başkası 403 `CASH_SESSION_OWNER_REQUIRED`. Mutabakat (reconcile): oturumun kasiyeri ve oturumu kapatan kişi mutabık edemez; yalnız G2'de eklenen ortak `approvals.manager_self_approval_enabled` ayarı AÇIKSA edebilir ve kayıt `reconciled_self_approved = true` ile `audit` olayında işaretlenir (ikinci ayar tanımı yoktur; ayarın açıklaması "nakit çıkış ve mutabakat" olarak güncellenir). Ret 403 `CASH_RECONCILE_SELF_NOT_ALLOWED`, Türkçe nedenli.
4. F0091: kapanıştan önce en az bir sayım kaydı bulunmalı (409 `CASH_COUNT_REQUIRED`) ve `ActualCash` en son kayıtlı sayıma eşit olmalı (409 `CASH_COUNT_MISMATCH`).
5. F0926 + F0420: sayım kaydı ve kapanış, oturum satırı `FOR UPDATE` kilidi altında, durum yeniden denetlenerek tek işlemde yapılır. Beklenen nakit kapanış işleminin içinde, kilit altında okunur. Eşzamanlı ikinci kapanış 409 alır (durum artık Closed).
6. ClosingDifference (karar): tasarım belgesi (`cash-session-design.md:96`) "fark kapanışta deftere yazılır" der ve defter tipi (`ClosingDifference`, beklenen nakde katılmaz) zaten tanımlıdır; kod yazmıyordu. Kod belgeye uyar: fark sıfırdan farklıysa kapanışla AYNI işlemde defter satırı yazılır (tutar = |fark|, fazla ise In, eksik ise Out, not = açıklama). Böylece defter tek başına `beklenen + işaretli fark = sayılan` ile okunur. Defteri okuyan tüm sorgular tip süzer (kontrol edilir ve testlenir).
7. Olay kararı: `CashSessionClosedEvent` dönüş değeridir, hiçbir yere yayımlanmıyordu. Tüketici yoktur (mutabakat farkı doğrudan `cash.cash_sessions.difference` sütunundan okur); `alkaros-boundary-event` sözleşmesine göre tüketicisiz olay eklenmez. Kapanış, kapanışla aynı işlemde yazılan `audit` olayıyla kayıt altındadır. Gerekçe RESULT.md'de.
8. Kapanış ekranının ihtiyacı: `GET .../expected-cash` yanıtı beklenen nakde ek olarak `varianceTolerance`, `openBillCount`, `unsettledPaymentCount` taşır (tek istek; uç adı değişmez).
9. Tasarım belgesi (F0353, F0408, satır 96): bayat "persistence V1.3'te" bölümü, kod yorumlarının "section 5/§3" atıfları, mutabakat yetkisi (`cash.session.override` izni; Accountant için PO kararı yoktur, belge kodu izler), ClosingDifference.
10. Türkçe hata cevapları `ApiErrorCatalog`'dadır; ham istisna metni sızmaz.

Kapsam dışı: arayüz (V1-RMD-792; A bittiği an mevcut kapanış ekranı fark açıklaması göndermediği için farklı kapanışları reddeder, 788→789 öncülü gibi 792 hemen ardından yapılır); gün sonu (V1-RMD-791); iade yolunun oturum kilidi (G4). Sunucuya özel not: kasiyer kimliği çerezli kasiyer oturumundan gelir (bireysel kullanıcı); paylaşılan kiosk kimliği olsaydı sahiplik kuralı bir şey denetlemezdi, bu RESULT'ta doğrulanıp yazılır.

## Owned surface

- `evidence/V1-RMD-790/**`
- `database/migrations/V1/V1-RMD-790/**`
- `src/Host/DualScreen/CashCloseStore.cs`
- `src/Modules/Settings/CashCloseTolerance/CloseVarianceToleranceSetting.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Cash/Contracts/CashSessionPolicy.cs (V1-CSH-001) — tolerans parametresi, sahiplik, açıklama, mutabakat kuralları.
  - src/Modules/Cash/Contracts/CashSessionCommands.cs (V1-CSH-001) — kapanış komutu alanları.
  - src/Modules/Cash/Contracts/CashSessionEvents.cs (V1-CSH-001) — açıklama alanı.
  - src/Modules/Cash/Contracts/CashSessionExceptions.cs (V1-CSH-001) — yeni istisnalar.
  - src/Modules/Cash/SessionLifecycle/CashSessionLifecycleService.cs (V13-CSH-001) — kilitli kapanış, sayım denetimi, defter satırı.
  - src/Modules/Cash/SessionLifecycle/ICashSessionLifecycleService.cs (V13-CSH-001) — sözleşme.
  - src/Modules/Cash/SessionLifecycle/ICashSessionRepository.cs (V13-CSH-001) — kilitli okuma/yazma sözleşmesi.
  - src/Modules/Cash/SessionLifecycle/PostgresCashSessionRepository.cs (V13-CSH-001) — kilitli okuma/yazma, yeni sütunlar.
  - src/Modules/Cash/SessionLifecycle/CashSessionRecord.cs (V13-CSH-001) — yeni alanlar.
  - src/Modules/Cash/SessionLifecycle/CashSessionLifecycleModule.cs (V13-CSH-001) — servis kaydı gerekirse.
  - src/Modules/Cash/TransactionLedger/ICashTransactionLedgerRepository.cs (V13-CSH-003) — kilit altında beklenen nakit okuma.
  - src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs (V13-CSH-003) — kilit altında beklenen nakit okuma.
  - src/Modules/Settings/ManagerSelfApproval/ManagerSelfApprovalSetting.cs (V1-RMD-788) — açıklama metni (mutabakat da).
  - src/Modules/Settings/TypedSettings/SettingValidator.cs (V1-RMD-683) — tolerans ayarı doğrulaması ve 500 TL tavan.
  - src/Host/Experience/Settings/SettingsManagementEndpoints.cs (V1-RMD-683) — ayarın kaydı.
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs (V13-CSH-004) — sahiplik, kapanış, mutabakat, beklenen nakit yanıtı.
  - src/Host/DualScreen/DualScreenApplication.cs (V1-GOV-008) — yeni deponun kaydı.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata kodları.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — yeni eşlemeler.
  - tests/Host/Experience/Settings/SettingsManagementHttpTests.cs (V1-RMD-683) — tolerans ayarı, tavan ve audit.
  - docs/architecture/settings-ownership.md (V1-RMD-686) — yeni ayar.
  - docs/domain/cash-session-design.md (V1-CSH-001) — ClosingDifference, mutabakat yetkisi, bayat bölümler.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004) — açıklamalı, sayımlı kapanışlar.
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs (V1-RMD-788) — kapanış çağıran testlerin uyumu.
  - tests/Host/Experience/CashSession/CashCloseSafetyHttpTests.cs (V13-CSH-004) — yeni dosya: kapanış güvenliği HTTP testleri.
  - tests/Modules/Cash/Contracts/CashSessionPolicyTests.cs (V1-CSH-001) — politika kuralları.
  - tests/Modules/Cash/Contracts/CashSessionLifecycleTests.cs (V1-CSH-001) — komut kuralları.
  - tests/Modules/Cash/SessionLifecycle/CashSessionLifecycleServiceTests.cs (V13-CSH-001) — servis ve depo davranışı.
  - tests/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepositoryTests.cs (V13-CSH-002) — ClosingDifference satırının beklenen nakdi bozmaması.
  - tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs (V13-CSH-003) — politika imzası değişirse uyum.
  - tests/Modules/Payments/EftTender/EftTenderHandlerTests.cs (V13-PAY-005) — depo sözleşmesi değişirse uyum.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kural için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil.
2. Eşzamanlı iki kapanış ve sayım/kapanış yarışı gerçek Postgres'te paralel isteklerle kanıtlanır.
3. Tolerans tam eşitse kapanır, 0,01 üstü yönetici onayı ister; tavan üstü ayar değeri reddedilir; ayar değişikliği audit'e düşer.
4. Açık hesap/belirsiz ödeme: onaysız 409, onayla kapanır ve audit'te iki sayı ve onay görünür; yalnız `Paid`/`Cancelled` hesap ve karara bağlanmış ödeme sayıya girmez.
5. Sahiplik: sahibi olmayan kasiyer 403, yönetici izinli kullanıcı geçer, sahibin normal kapanışı yetki ret kaydı üretmez.
6. Mutabakat: kapatan ve sahibi engellenir, ayar açıkken işaretli geçer, üçüncü yetkili geçer.

## Out of scope

- Arayüz (V1-RMD-792); gün sonu (V1-RMD-791); iade/kart onayı (G4); oturumun kullanıcı başına tek olması (çok terminalli kullanımı bozar).

## Dependencies

- V1-RMD-789

## Acceptance evidence

1. Kırmızı sonra yeşil: Host (CashSession, Settings), Cash modülü ve ApiConventions testleri küçük gruplarla koşulur.
2. Migration ileri ve geri boş veritabanında çalışır (`evidence/V1-RMD-790/`).
3. Mutasyon: her kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. ApiConventions, ModuleBoundaries yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir, düzeltmelerden sonra kısa doğrulama turu yapılır.

## Handoff

- V1-RMD-791
- V1-RMD-792
