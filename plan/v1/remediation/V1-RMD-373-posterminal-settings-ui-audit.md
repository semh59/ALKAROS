# V1-RMD-373 - PosTerminal Ayarlar ekranları modül denetimi: axe taraması + eksik test dosyası

- Task ID: V1-RMD-373
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 13. modülü:
PosTerminal'in yedi Ayarlar ekranı (`RelaySettings`, `QnbCredentialSettings`,
`TokenTerminalSettings`, `SecurityAdministration`, `BusinessIdentitySettings`,
`ReservationStation`, `CustomerDisplayScreensaverSettings` — `src/Clients/PosTerminal/src/routes/**`,
toplam ~1770 satır). On iki boyut üzerinden tarandı.

Bu ekranların hiçbiri kendi özel hata sınıfını fırlatmıyor (hepsi paylaşılan `api.ts`
üzerinden `ApiError` kullanıyor) — Modül 7/8/9/12'nin sistemik "yanlış hata sınıfı" bulgusu
burada UYGULANAMAZ, çünkü ortada ikinci bir sınıf yok. Altı ekranın hepsi zaten kendi test
dosyasına sahip ve iyi kapsanmış (giriş formu, yetkisiz erişim, form doğrulama, sunucu
çakışması senaryoları).

İki bulgu:

1. **[Modül 5/10/12 sınıfı] Yedi ekranın HİÇBİRİNDE axe-core taraması yoktu.**
2. **`CustomerDisplayScreensaverSettings.tsx`'in (262 satır — ekran koruyucu yükleme/kaldırma)
   hiçbir test dosyası bile yoktu** — altı kardeş ekranın hepsi test edilirken bu biri hiç
   test edilmemişti.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/RelaySettings.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/TokenTerminalSettings.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/SecurityAdministration.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.test.tsx
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.test.tsx
- `src/Clients/PosTerminal/src/routes/CustomerDisplayScreensaverSettings.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-373-posterminal-settings-ui-audit.md`

## In scope

1. **[T1, Yüksek — test kapsamı] Yedi ekranın hiçbirinde axe-core taraması yoktu.** Her birine,
   zaten oturum açmamış durumu (giriş formu) sahneleyen aynı basit desenle bir axe testi
   eklendi.
2. **[T1, Yüksek — test kapsamı] `CustomerDisplayScreensaverSettings.tsx`'in hiç test dosyası
   yoktu.** Yeni dosya: giriş formu, yetkisiz erişim (catalog.manage olmadan), dosya boyutu
   doğrulaması (5 MB üstü görsel reddi) ve axe taraması kapsandı.

## Out of scope

- Üretim kodu değişmedi — hiçbir ekranda gerçek bir davranış hatası bulunmadı, yalnızca
  test-kapsama boşlukları kapatıldı.
- Ürün-katmanı (P1-P4) gözlemi yok.

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (38
  dosya, 288 test, bu görevin 10 yeni testi dahil): 288/288 geçti, regresyon yok.
- Üretim kodu değişmediği için mutation-check gerekmedi (Modül 10/12'nin axe-only kapanışlarında
  olduğu gibi) — yeni testler kendi kendini doğruluyor (gerçekten bozuk bir bileşende
  başarısız olurlardı).

## Handoff

- None
