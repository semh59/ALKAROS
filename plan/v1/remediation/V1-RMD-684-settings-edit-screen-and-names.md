# V1-RMD-684 - Ayar düzenleme ekranı, geçmişte kişi adları ve istemci düzeltmeleri

- Task ID: V1-RMD-684
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları R2, R3 ve R12a-g kapanır. Müdür, ayar geçmişi ekranında satıra dokunup değeri değiştirir (gerekçe zorunlu, Türkçe aralık hatası, sürüm çakışmasında yenile uyarısı); garson masa sınırı ayarı ilk açılışta kayıtlı gelir. Geçmişte değiştiren kişi adıyla görünür. İşletme kimliği ekranında yükleme ve kayıt hataları, oturum bitişi, logo kaldırma onayı ve rowVersion davranışı düzelir; Duration ve tam sayı değerler birimle, saat Istanbul diliminde gösterilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-684-settings-edit-screen-and-names.md`
- `evidence/V1-RMD-684/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.tsx - yalnız düzenleme ve ad gösterimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.test.tsx - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/models.ts - yalnız birim, saat dilimi ve çeviri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/api.ts - yalnız yazma çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/management-settings-history.css - yalnız stil
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.tsx - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.test.tsx - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/panel.tsx - yalnız iptal bayrağı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx - yalnız hata, oturum, onay ve sürüm davranışı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.test.tsx - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız ayar metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/styles.css - yalnız dokunma hedefi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/SettingsManagementEndpoints.cs - yalnız geçmişe görünen ad
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/SettingsManagementContracts.cs - yalnız ad alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/WaiterMaxActiveTables/WaiterMaxActiveTablesSetting.cs - yalnız açılışta kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/SettingsModule.cs - yalnız açılışta kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/SettingsManagementHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-683-settings-value-validation.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.
- V1-RMD-739 doğrulama notu (2026-10-28): src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.test.tsx yolları HEAD'de bulunmaz; bu görev tarihsel olarak kapalı kalır.

## In scope

- Düzenleme ekranı, ad çözümü, ayarların açılışta kaydı, istemci hata ve oturum düzeltmeleri, birim ve saat dilimi, testler (axe dahil).

## Out of scope

- Garson seçicide sınır rozeti (ayrı iş, ürün kararı); SellerProfileCard (Invoicing sahibi).

## Dependencies

- V1-RMD-683

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-684/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-685
