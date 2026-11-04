# V1-RMD-793 - İade (backend): doğru iade yöntemi, terminale bağlı oturum, "kendi onayı" ayarı

- Task ID: V1-RMD-793
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G4 grubunun iade tarafı (`7-birlesik-dogrulama.md` satır 20, 21, 2 iade/kart kısmı; satır 11 ve "slip önce doğrulanıyor" kusuru HEAD'de kapalı çıktı, aşağıda). PO kararları aynen uygulanır:

1. İade yöntemi (satır 20, `PaymentRefunds.cs:160-167`): nakit dışı her ödeme `Manual` yazılıyordu. Yöntem artık kanıta bakılarak bulunur: `Account` (cari borç satırı), `Cash` (kasa defterinde Sale satırı), `Card` (`card_settlement_attempts` onaylı satırı VEYA onaylanmış elle kart bildirimi), kalan `Eft`. "Kalan = Eft" yalnız şu nedenle doğrudur: onaylı ödeme üreten yer kümesi kapalıdır (4 `new Payment(` yeri: nakit, cari, kart, EFT + elle kart onayı); yemek kartının işleyicisi kayıtlı değildir. Bu gerekçe kodda yorum olarak yazılır. Tek kaynak: `PaymentMethodLookup` (yeni); iade ve fiş aynı sözlüğü kullanır (fiş `DualScreenApplication.Receipt.cs:171-190` kendi kopyasını taşıyor ve elle onaylanan kartı `Manual` = "EFT / diğer" gösteriyordu; o da düzelir). `refund_requests.method` değerleri `Cash | Card | Eft`; `Manual` değeri geçiş 215'te eski satırlar aynı kuralla geri doldurularak kaldırılır, ölü değer kalmaz. Tamamlama alanları: `Card` fiş numarası ister; `Eft` banka referansı İSTEMEZ (EFT'nin doğrulamasız onayı PO kararı 2026-09-16'dır, dokunulmaz), yalnız "havale yapıldı" onayı ve isteğe bağlı not.
2. Terminal/oturum eşleşmesi (satır 21, `PaymentRefunds.cs:200,360,531-532`): iade isteği hangi terminalden geldiyse `terminal_id` olarak saklanır; nakit iade o terminaldeki, isteyenin açık oturumundan düşer (terminal, kullanıcı). "Kullanıcı başına tek aktif oturum" kısıtı KONMAZ. Aynı eşleşme iade bütçesinde (`GetAllowanceAsync`, `PostgresRefundAllowanceRepository.SetAsync`) de geçerlidir; bütçe verme gövdesine `CashierTerminalId` eklenir. Terminali bilinmeyen eski satır (geçiş öncesi) nakit onayında `RefundNoOpenCashSession` ile reddedilir (yeniden istenir); geçiş eski satırları kasiyerin o saatteki oturumundan geri doldurmaya çalışır.
3. "Kendi onayı" (PO kararı 1): V1-RMD-788'in ortak `approvals.manager_self_approval_enabled` ayarı (ikinci tanım YOK). Kapalıyken ikinci yetkili şarttır (bugünkü `RefundSelfDecisionException`, Türkçe mesaj kuralı açıkça söyler); açıkken iade isteyen kişi kendi iadesini onaylar/reddeder, kendine iade bütçesi verebilir ve elle kart "çekildi" bildirimini kendisi onaylar. Kayıt işaretlenir (`refund_requests.approval_mode = 'Self'`, `manual_card_confirmations.self_approved`), audit olayına `selfApproved` yazılır, iade listesi `selfApproved` ile süzülür. Ayarın açıklaması ("in a later task" ifadesi) güncel davranışa göre yeniden yazılır.
4. Oturum kilidi (c, G2 raporundan): nakit iade satırı kasa oturumu satırını `FOR SHARE` kilitler ve oturum Open değilse reddeder (V1-RMD-790, `evidence/V1-RMD-790/n1-refundlock`; kapanış `FOR UPDATE` alır). HEAD'de kapalıdır; bu görev yeni terminal-kapsamlı yolun ve bütçe harcama yolunun da aynı kilit altında kapanışla yarışmadığını gerçek Postgres yarış testiyle KANITLAR (kod değişikliği gerekirse yapılır). F0917 (kısmi iadenin kasa kapanış beklenen nakdine etkisi hiç denenmemişti) aynı testlerle kapanır.
5. Kesirli miktar: iade tarafındaki `PriceItemsAsync` miktar kuralı (en çok 3 ondalık, V1-RMD-146 davranışı) değişmez; G5'te gelecek ürün bayrağı bu noktada bağlanacağı için bozulmaz.

HEAD'de kapalı çıkanlar (kod değişmez, testle atıf RESULT.md'de): satır 11 `EftAmountMismatchException` (`EftTenderHandler.cs:99-104`); "fiş numarası durum denetiminden önce doğrulanıyor" kusuru (`PaymentRefundHttpTests.cs:155` testi, durum denetimi önce); F0389 nakit hareketi Notes tekrarı (V1-RMD-788, `CashMovementStore.cs:160`).

Kapsam dışı: ödeme tarafı (V1-RMD-794), arayüzler (V1-RMD-795), F0094/F0095/F0450 ödeme-uzlaşma raporu ve F0347 belge notu (G10/G11; 9-1020 dosyası bunları G4'e almıyor), gün sonu raporuna "kendi onayı" sayacı (iade listesi süzgeci ve audit yeterli; gün sonu rapor şekli V1-RMD-791'in).

## Owned surface

- `evidence/V1-RMD-793/**`
- `database/migrations/V1/V1-RMD-793/**`
- `src/Host/Experience/Billing/PaymentMethodLookup.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/RefundMethodHttpTests.cs (V1-RMD-508) — yeni dosya: iade yöntemi testleri.
  - tests/Host/Experience/Billing/RefundTerminalSessionHttpTests.cs (V1-RMD-508) — yeni dosya: terminal/oturum ve kilit testleri.
  - tests/Host/Experience/Billing/RefundSelfApprovalHttpTests.cs (V1-RMD-508) — yeni dosya: kendi onayı testleri.
  - src/Host/Experience/Billing/PaymentRefunds.cs (V1-RMD-508) — yöntem, terminal, kendi onayı, tamamlama alanları.
  - src/Host/Experience/Billing/BillingSplitApplication.cs (V1-RMD-103) — uç noktalar, ayar okuma, hata eşlemesi, `selfApproved` süzgeci, bütçe gövdesi.
  - src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs (V1-RMD-508) — yeni sütunlar, süzgeç.
  - src/Modules/Cash/SessionLifecycle/PostgresRefundAllowanceRepository.cs (V1-RMD-520) — (terminal, kullanıcı) eşleşmesi.
  - src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs (V1-RMD-283) — kendi onayı.
  - src/Modules/Payments/ManualResolution/ManualCardConfirmation.cs (V1-RMD-283) — `SelfApproved` alanı.
  - src/Modules/Payments/ManualResolution/PostgresManualCardConfirmationRepository.cs (V1-RMD-283) — sütun.
  - src/Modules/Payments/ManualResolution/IManualCardConfirmationRepository.cs (V1-RMD-283) — sözleşme.
  - src/Host/DualScreen/DualScreenApplication.Payments.cs (V1-RMD-258) — elle kart onayı ucu ayar okur, audit'e `selfApproved`.
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs (V1-RMD-516) — fişte ortak yöntem sözlüğü.
  - src/Modules/Settings/ManagerSelfApproval/ManagerSelfApprovalSetting.cs (V1-RMD-788) — açıklama metni.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — hata eşlemesi gerekirse.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Host/Experience/Billing/PaymentRefundHttpTests.cs (V1-RMD-508) — yöntem adları ve yeni kurallara uyum.
  - tests/Host/Experience/Billing/BillingSplitHttpTests.cs (V1-RMD-103) — sözleşme uyumu.
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs (V1-RMD-516) — fiş yöntem adı.
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs (V1-RMD-258) — elle kart onayı uyumu.
  - tests/Host/Experience/Reconciliation/ManualCardConfirmationConstraintTests.cs (V1-RMD-359) — dört göz kısıtı değişir.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — hata eşlemesi gerekirse.
  - docs/domain/refund-ledger.md (V0-DOM-003) — iade yöntemi ve kendi onayı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil; mutasyon turu.
2. Geçiş 215: `refund_requests.terminal_id` (null yalnız eski satırlar), `method` kısıtı `Cash|Card|Eft` (eski `Manual` satırlar kanıta göre geri doldurulur), `approval_mode` kısıtına `Self`, "karar veren ≠ isteyen" kısıtı `Allowance|Self` modlarına izin verir, `manual_card_confirmations.self_approved` (varsayılan false) ve dört göz kısıtı `self_approved` ile genişler. İleri, geri, tekrar ileri.
3. Gerçek Postgres testleri: yöntem (nakit, kart onaylı, elle onaylı kart, EFT, cari), tamamlama alanları (Card fişsiz 400, Eft fişsiz tamamlanır), terminal eşleşmesi (aynı kullanıcının iki terminalinde açık oturum: iade istenen terminalinkinden düşer; o terminalde oturumu olmayan hata alır), bütçe (terminal, kullanıcı), kapalı ayar ret + açık ayar kabul (onay, ret, kendine bütçe, kart onayı), işaretleme (satır, audit, süzgeç), kapanışla yarış (onay yolu, bütçe yolu), F0917 kısmi iade beklenen nakdi düşürür.
4. İngilizce kod ve test adları; kullanıcıya dönen hata metinleri Türkçe.

## Out of scope

- Tahsilat ve ödeme hata yolları (V1-RMD-794); iade ve fiş istemcileri (V1-RMD-795); ödeme-uzlaşma raporu.

## Dependencies

- V1-RMD-788
- V1-RMD-790

## Acceptance evidence

1. Kırmızı sonra yeşil: yeni üç test dosyası + `PaymentRefundHttpTests`, `BillReceiptHttpTests`, `PaymentTenderHttpTests`, `ManualCardConfirmationConstraintTests`, `ManifestTests`, `ApiConventions`, `ModuleBoundaries`, `ApiErrorCatalogTests` (gerçek Postgres, `evidence/V1-RMD-793/`).
2. Migration 215 ileri/geri/ileri kanıtı.
3. Mutasyon: her yeni kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-backend ve qa-test-kalitesi incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
6. Semih'in elle deneyeceği senaryo: ayarlar ekranında "Yönetici kendi talebini onaylayabilir" kapalıyken kendi iade isteğini onaylamayı dene (reddedilir, nedeni yazar); aç, onayla (kayıt "kendi onayı" işaretli); EFT ile ödenmiş hesapta iade iste: "Havale / EFT" görünür, fiş numarası sorulmaz; aynı kullanıcı iki terminalde oturum açmışken iade, iadenin istendiği terminalin kasasından düşer.

## Handoff

- None
