# V1-RMD-582 - QNB e-Arşiv fatura gönderim istemcisi

- Task ID: V1-RMD-582
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Online sipariş faturaları alıcısı e-Fatura mükellefi olmayan müşteriye kesildiği için e-Arşiv'dir; repoda e-Arşiv gönderimi için istemci ve görev yoktu (yalnız e-Fatura taslağı vardı). QNB'nin herkese açık WSDL/XSD'si ve API belgesindeki "e-Arşiv Fatura Gönderme" bölümüne göre `faturaOlusturExt` çağrısını yapan istemci eklenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-582-qnb-earsiv-send-client.md`
- `evidence/V1-RMD-582/**`
- `src/Modules/Invoicing/Qnb/EArsiv/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/Qnb/Client/QnbSoapClient.cs - yalnız ortak SOAP çağrısını paylaşan iç yardımcı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Invoicing/Qnb/Client/** - yalnız yeni test dosyası
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- e-Arşiv servisine giriş (kendi sunucusunda, `earsiv/ws/userService`) ve `faturaOlusturExt`: giriş JSON'u (`islemId`, `vkn`, `sube`, `kasa`, `erpKodu`, `donenBelgeFormati`), UBL belge içeriği base64, sonuç kodu `AE00000` dışındaysa hata.
- Cevaptan fatura numarası, UUID ve görüntüleme adresi okunur.

## Out of scope

- Sorgulama, iptal, itiraz, rapor (belgede giriş şeması yayımlanmamış); UBL-TR fatura XML'i üretmek (V1-RMD-470..475 zinciri); gerçek hesapla doğrulama (V0-QNB-001 Blocked).

## Dependencies

- V14-QNB-007

## Acceptance evidence

1. Sahte SOAP sunucusuyla istek biçimi ve cevap okuma testleri; mutasyon kanıtı `evidence/V1-RMD-582/` altındadır.
2. Gerçek QNB hesabıyla denenmediği README'de açıkça yazılır.

## Handoff

- None
