# V1-RMD-471 - Fatura için satıcı (işletme) bilgileri ayarı

- Task ID: V1-RMD-471
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Faturanın satıcı bölümü (ticari ünvan, VKN, vergi dairesi, adres) bugün hiçbir yerde saklanmıyor; yalnız işletme adı
ayarı ve QNB kimlik bilgisindeki VKN var. `V1-RMD-470` tabloyu açar; bu görev bilgileri okuyup yazan uç noktaları ve Yönetim
ekranındaki "Fatura bilgileri" bölümünü ekler. VKN 10 haneli, TCKN 11 haneli doğrulanır; eksik bilgiyle fatura taslağı açılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-471-seller-profile-settings.md`
- `src/Host/Experience/InvoiceSettings/**`
- `tests/Host/Experience/InvoiceSettings/**`
- `src/Clients/PosTerminal/src/features/management-invoice-settings/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/sections.ts - yalnız yeni bölümün kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni bölümün Türkçe metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ManagementArea.test.tsx - yalnız bölüm listesi beklentisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız yeni uç noktaların kaydı

## In scope

- `GET`/`PUT` satıcı bilgileri (yetki: `integrations.manage`), doğrulama, Türkçe hata gerekçeleri; Yönetim bölümü (yükleniyor, boş, hata).
- Testler: uç nokta (yetki, doğrulama, kalıcılık), istemci (Türkçe, axe). Uç nokta gelince `ISellerProfileStore` erişilebilir olur ve `V1-RMD-470` içindeki geçici izin listesi kaydı kalkar.

## Out of scope

- Fatura taslağı üretimi (`V1-RMD-470`); QNB kimlik bilgileri (mevcut ekran).

## Dependencies

- V1-RMD-470

## Acceptance evidence

- Testler ve gerçek Host denemesi (bilgi kaydedilir, yeniden açılınca görünür); çıktılar `evidence/V1-RMD-471/` altındadır.

## Handoff

- None
