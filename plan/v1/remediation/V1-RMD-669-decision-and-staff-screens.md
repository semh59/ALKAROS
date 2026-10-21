# V1-RMD-669 - Yetki karar ekranı ve personel ekranının hata, onay ve okunurluk düzeltmeleri

- Task ID: V1-RMD-669
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B005, B006 (adlar hariç) ve B029 kapanır. Yetki karar ekranında onayla, reddet, devri geri al ve sıkılaştırmayı kaldır işlemleri hata verirse ekran boşalmaz ya da sessiz kalmaz; Türkçe bir ileti gösterir, işlem sürerken düğmeler kapanır ve liste yenilenirken mevcut içerik kalır. Talepler rolün Türkçe adıyla, İstanbul saatiyle tarih ve saat olarak gösterilir; 12 sebep kodunun hepsi Türkçe etiket alır. Personel ekranında pasifleştirme onay ister, işlem sonrası kart yeniden okunur, form temizlenir ve satırlar parmakla basılacak boyuttadır.

## Owned surface

- `plan/v1/remediation/V1-RMD-669-decision-and-staff-screens.md`
- `evidence/V1-RMD-669/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx - yalnız hata, meşgul durumu, okunurluk
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.test.tsx - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/models.ts - yalnız etiketler ve metinler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/authorization-decisions.css - yalnız hata ve meşgul stili
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız yetki karar rotasının eylem sarmalayıcısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.tsx - yalnız kart yenileme, onay, form temizleme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.test.tsx - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/management-staff-roles.css - yalnız satır yüksekliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız personel onay metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-670-grant-amount-and-approver-rules.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- B005: dört karar eyleminde hata yakalama, Türkçe hata iletisi, işlem sürerken kapalı düğmeler, sessiz yenileme.
- B006: rol adı, tr-TR İstanbul tarih ve saat, 12 sebep etiketi.
- B029: pasifleştirme onayı, işlem sonrası kart yenileme, hesap açıldıktan sonra form temizleme, 48 px satır.

## Out of scope

- Talep eden, devralan ve kullanıcı adlarının sunucudan gelmesi (sözleşme alanı ve kullanıcı sorgusu gerekir; V1-RMD-672). Tarayıcıda elle deneme (doğrulanamadı olarak belgelenir).

## Dependencies

- V1-RMD-670

## Acceptance evidence

1. Her kural için vitest testi; hata yolu için mutasyon kanıtı.
2. `evidence/V1-RMD-669/` altında çıktılar; ilgili vitest dosyaları ve tip denetimi yeşil.

## Handoff

- V1-RMD-598
