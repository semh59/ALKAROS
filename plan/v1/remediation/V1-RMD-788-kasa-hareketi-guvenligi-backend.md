# V1-RMD-788 - Kasa hareketi güvenliği (backend): üst sınır, zorunlu gerekçe, eşik üstü yönetici onayı, aynı kişi onayı ayarı

- Task ID: V1-RMD-788
- Status: Planned
- Assignee: unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 1. satırı (`7-birlesik-dogrulama.md`, koddan doğrulandı; kasa hareketinde bugün yalnız `cash.drawer` ve `amount > 0` aranır) ve 1.020 kayıtlı doğrulama programından kasa hareketine bağlı F0389 (tekrar karşılaştırması Notes alanını katmıyor) kapanır. PO kararları (2026-11-03) aynen uygulanır:

1. Üst sınır: nakit çıkış kasadaki beklenen nakitten (açılış + satış + giriş - çıkış - iade) fazla olamaz. Denetim ve ekleme tek işlemde, oturum satırında dışlayıcı kilit altında yapılır; iki eşzamanlı çıkış birlikte sınırı aşamaz. Aşım 409 `CASH_OUT_EXCEEDS_DRAWER`.
2. Zorunlu gerekçe: nakit giriş ve çıkışın açıklaması boş olamaz (kırpılmış 1-500 karakter); ihlal 400 `CASH_MOVEMENT_REASON_INVALID`. Kural alanda (`CashTransaction`) ve veritabanında (yeni satırlar için `NOT VALID` denetim kısıtı; eski satırlar dokunulmaz) durur.
3. Eşik üstü yönetici onayı: çıkış tutarı eşikten kesin büyükse kayıt defterine yazılmaz; "Requested" durumunda bir çıkış talebi açılır (202). Talebi `cash.session.override` iznine sahip, talebi açandan farklı bir kullanıcı onaylar (onayda kayıt defterine yazılır, sınır onay anında yeniden denetlenir) ya da reddeder; talep sahibi vazgeçebilir. Bekleyen talep varken kasa kapatılamaz (409 `CASH_MOVEMENT_REQUESTS_PENDING`).
4. Eşik yönetici ayarıdır: `cash.cash_out_approval_threshold` (WholeNumber, TL, 0-1.000.000, varsayılan 500). Varsayılan 500 TL: günlük küçük giderlerin (su, ekmek, kurye) onaysız, tedarikçi ödemesi ve avans gibi büyük çıkışların onaylı olması varsayımı; yönetici değiştirir. 0 = her çıkış onay ister.
5. Aynı kişi onayı: yönetici kendi çıkış talebini onaylayamaz; yalnız ortak ayar `approvals.manager_self_approval_enabled` (Toggle, varsayılan KAPALI) AÇIKSA onaylayabilir ve talep `self_approved = true` olarak işaretlenir. Ayar tek yerde (`ManagerSelfApprovalSetting`) tanımlanır; G4 iadede aynı sınıfı kullanacaktır (bu görev iadeye bağlamaz).
6. Audit: çıkış talebinin açılması, onayı, reddi ve vazgeçilmesi aynı işlemde `audit` kaydı yazar. Ayar değişikliği (eşik ve kendi onayı dahil, her ayar) yönetim ucunda `settings.value_changed` / `settings.deactivated` audit olayı yazar (eski ve yeni değer, gerekçe, kullanıcı); ayar geçmişi tablosu zaten değişmezdir.
7. Tekrar (idempotent) karşılaştırması: aynı anahtar farklı yön, tutar ya da açıklamayla gelirse 409 `IDEMPOTENCY_KEY_REUSED` (F0389). Eşik üstü talep de aynı anahtarla tekrar edilebilir ve yeni satır açmaz.
8. Yeni `GET .../cash-movements`: oturumun nakit giriş/çıkış hareketleri (kim, ne zaman, tutar, açıklama, onaylayan, kendi onayı işareti), bekleyen talepler, geçerli eşik ve kendi onayı ayarı; arayüz görevi (V1-RMD-789) bunu kullanır.
9. Türkçe hata cevapları katalogdadır (`ApiErrorCatalog`); ham istisna metni sızmaz.
10. Mevcut testlerdeki gerekçesiz nakit giriş/çıkışlar gerekçeli ve sınır içinde olacak şekilde uyarlanır (kural gevşetilmez).

Kapsamda doğrulama (yeniden yapılmaz): satır 12 (tutar ölçeği) ve satır 7 V1-RMD-787'de kapandı; kasa hareketi akışıyla uyumu `ACashMovementTheLedgerCannotStoreExactlyIsRefusedAsAValidationError` ve yeni testlerin ölçek/sınır durumlarıyla doğrulanır.

Kapsam dışı: F0409 (`cash-tender` tekrarında `TenderedAmount` karşılaştırılmıyor) kasa hareketi değil nakit satış yoludur ve bulgu "etkisi yok" der (para üstü aynı döner); tahsilat grubunda (G4) kalır. Arayüz (cash-session.js, E2E) V1-RMD-789. `Refund` satırlarının oturum kilidini almaması (iade yolu) G4 riski olarak RESULT'ta notlanır.

## Owned surface

- `evidence/V1-RMD-788/**`
- `database/migrations/V1/V1-RMD-788/**`
- `src/Host/DualScreen/CashMovementStore.cs`
- `src/Host/DualScreen/DualScreenApplication.CashMovements.cs`
- `src/Modules/Settings/CashMovementApproval/CashOutApprovalThresholdSetting.cs`
- `src/Modules/Settings/ManagerSelfApproval/ManagerSelfApprovalSetting.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Cash/TransactionLedger/CashMovementRequest.cs (V13-CSH-002) — yeni dosya: çıkış talebi modeli.
  - src/Modules/Cash/TransactionLedger/ICashMovementRepository.cs (V13-CSH-002) — yeni dosya: talep ve hareket listesi deposu sözleşmesi.
  - src/Modules/Cash/TransactionLedger/PostgresCashMovementRepository.cs (V13-CSH-002) — yeni dosya: talep ve hareket listesi deposu.
  - tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs (V13-CSH-004) — yeni dosya: onay akışı HTTP testleri.
  - src/Modules/Cash/TransactionLedger/CashTransaction.cs (V1-RMD-241) — gerekçe kuralı.
  - src/Modules/Cash/TransactionLedger/CashTransactionExceptions.cs (V13-CSH-002) — yeni istisnalar.
  - src/Modules/Cash/TransactionLedger/ICashTransactionLedgerRepository.cs (V13-CSH-003) — kilit altında beklenen nakit okuma.
  - src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs (V13-CSH-003) — kilit altında beklenen nakit okuma.
  - src/Modules/Cash/TransactionLedger/CashTransactionLedgerModule.cs (V13-CSH-002) — yeni depo kaydı.
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs (V13-CSH-004) — hareket ucu yeni depoya, kapanışta bekleyen talep denetimi.
  - src/Host/DualScreen/DualScreenApplication.cs (V1-GOV-008) — yeni deponun kaydı.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata kodları ve alan adları.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — yeni eşlemeler.
  - src/Modules/Settings/TypedSettings/SettingValidator.cs (V1-RMD-683) — iki yeni ayarın doğrulaması.
  - src/Host/Experience/Settings/SettingsManagementEndpoints.cs (V1-RMD-683) — ayarların kaydı ve ayar değişikliği audit olayı.
  - tests/Host/Experience/Settings/SettingsManagementHttpTests.cs (V1-RMD-683) — ayar listesi ve audit testi.
  - docs/architecture/settings-ownership.md (V1-RMD-686) — iki yeni ayar.
  - docs/domain/cash-session-design.md (V1-CSH-001) — hareket kuralları.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Architecture/ApiConventions/idempotency_allowlist.json (V1-RMD-430) — yeni durum makinesi uçları.
  - tests/Architecture/ApiConventions/api_convention_allowlist.json (V1-RMD-429) — yeni uçlar gerekirse.
  - tests/Host/Experience/CashSession/CashSessionHttpTests.cs (V13-CSH-004) — gerekçeli ve sınır içi hareketler, kural testleri.
  - tests/Modules/Cash/TransactionLedger/CashTransactionTests.cs (V13-CSH-002) — gerekçe kuralı.
  - tests/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepositoryTests.cs (V13-CSH-002) — gerekçeli giriş/çıkış.
  - tests/Modules/CustomerAccounts/CashReceipts/CashAccountReceiptHandlerTests.cs (V1-RMD-442) — gerekçe kuralıyla uyum.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kural için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil.
2. Eşzamanlı iki çıkışın birlikte kasadaki parayı aşamaması gerçek Postgres'te iki paralel istekle kanıtlanır.
3. Eşik tam eşitse onaysız, eşiğin 0,01 üstü onaylıdır.
4. Onay: talep sahibi onaylayamaz (ayar kapalı), ayar açıkken onaylar ve `self_approved` işaretlenir, izinsiz kullanıcı 403, başka terminalin oturumu 404, durumu "Requested" olmayan talep 409, onayda kasa yetmiyorsa 409.

## Out of scope

- Arayüz (V1-RMD-789); iade ve kart onayı (G4); kasa kapanış toleransı (G3); iade yolunun oturum kilidi (G4 riski).

## Dependencies

- V1-RMD-787

## Acceptance evidence

1. Kırmızı sonra yeşil: Host (CashSession, Settings), Cash/Ledger ve ApiConventions testleri küçük gruplarla koşulur.
2. Migration ileri ve geri boş veritabanında çalışır (`evidence/V1-RMD-788/`).
3. Mutasyon: her kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. ApiConventions, ModuleBoundaries yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir.
6. Semih'in elle deneyeceği senaryo: kasa açıkken 600 TL nakit çıkış denenir; açıklama olmadan reddedilir, açıklamayla "yönetici onayı bekliyor" olur; kasadaki paradan büyük çıkış reddedilir.

## Handoff

- V1-RMD-789
