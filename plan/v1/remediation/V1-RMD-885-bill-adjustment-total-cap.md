# V1-RMD-885 - Hesap başına toplam ikram+indirim sınırı (yetki motoruna sınır, aşılınca yönetici onayı)

- Task ID: V1-RMD-885
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Ürün sahibi kararı (Semih, 2026-11-07): ikram ve indirim için hesap başına toplam üst sınır eklenir, ayarlanabilir olur, aşılınca yönetici onayı gerekir. Bugün her istek yalnız kendi rolünün tek-istek limitine bakıyor (kasiyer ikram 50 TL x 5 adet / 8 saat, garson indirim 100 TL x 10 adet / 8 saat): aynı hesapta 5 x 50 TL ikram parça parça yapılabiliyor ve garson indirimle ikramdan gevşek sınıra ulaşabiliyor. Yeni ön-politika kapısı (`IPrePolicyGate`, davranışsal sıkılaştırma kapısıyla aynı mekanizma): aynı hesap için daha önce verilmiş (granted) `bills.comp` ve `bills.discount` hibelerinin toplamı artı bu istek, ayarlı sınırı aşıyorsa otomatik onay yerine yönetici onayına düşer (bekleyen istek). Sınır ayarı `billing.adjustment_total_cap` (tam TL, yönetim ayarlar ekranından değişir); varsayılanı mevcut kişisel ikram bütçesinin günlük tavanıdır (`PersonalCompBudgetPolicy.DailyCap`, 150 TL), yeni sihirli sayı yoktur. Hesap başına eşzamanlı isteklerin sınırı birlikte aşmaması için hesap anahtarlı danışma kilidi alınır. Davranış değişikliği: bir hesapta kasiyer/garson yetkisiyle verilen toplam ikram+indirim 150 TL'yi (ayarlanabilir) geçerse yönetici onayı istenir; yönetici/izne sahip roller etkilenmez.

## Owned surface

- `evidence/V1-RMD-885/**`
- `src/Host/Experience/Billing/SettingsBillAdjustmentTotalCap.cs`
- `src/Modules/Settings/BillAdjustmentCap/BillAdjustmentTotalCapSetting.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Identity/Authorization/Grants/BillAdjustmentTotalGate.cs — yeni kapı
  - src/Modules/Identity/Authorization/Grants/IBillAdjustmentTotalCap.cs — sınır kaynağı arayüzü
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs — hesap toplamı sorgusu
  - src/Modules/Identity/Authorization/Grants/PostgresAuthorizationGrantRepository.cs
  - src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs — hesap kilidi
  - src/Host/Experience/Billing/BillingSplitApplication.cs — kapı ve sınır kaydı
  - src/Modules/Settings/TypedSettings/SettingValidator.cs — ayar aralığı
  - src/Host/Experience/Settings/SettingsManagementEndpoints.cs — ayar kaydı
  - src/Clients/PosTerminal/src/features/management-settings-history/models.ts — ayar etiketi ve düzenleme aralığı
  - src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx
  - tests/Modules/Identity/Authorization/Grants/BillAdjustmentTotalGateTests.cs — yeni
  - tests/Host/Experience/Billing/BillAdjustmentTotalCapHttpTests.cs — yeni
  - tests/Host/Experience/Settings/SettingsManagementHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Hesap toplamı sorgusu, kapı, hesap kilidi, sınır ayarı ve kayıtları
2. Gerçek Postgres testleri: parça parça ikram, indirim+ikram birlikte, ayar değişince sınır, yönetici onayı yolu, eşzamanlı istekler
3. Ayar ekranında etiket ve aralık
4. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Garsonun kalem ikramı (`OrderItem` konulu hibe, kendi kişisel bütçe tavanıyla): hesap başına toplama katılmaz, kalem-hesap eşlemesi ayrı iştir
- Geri alınan (undo) ikram/indirimlerin toplamdan düşülmesi: geri alınanlar toplamda sayılmaya devam eder (tutucu yön)
- Tek-istek limitleri ve politika tablosu

## Dependencies

- None

## Acceptance evidence

1. Kırmızı/yeşil test çıktıları
2. Mutasyon sonuçları
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
