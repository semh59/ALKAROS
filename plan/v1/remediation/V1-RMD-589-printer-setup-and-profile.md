# V1-RMD-589 - Yazıcı ekleme, bağlantı ayarları ve yazıcı başına model profili

- Task ID: V1-RMD-589
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: feature
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Mutfak yazıcısı oluşturmanın veya IP/port ayarlamanın hiçbir yolu yok: arayüz ve API yalnız mevcut yazıcının adını, istasyonunu ve aktifliğini değiştirir; "Yapılandırılmış yazıcı yok" ekranı yönetici için çıkmaz sokaktır ve yazıcı bugün yalnızca veritabanına elle SQL yazarak eklenebilir. Ayrıca Türkçe karakter tablosu tek bir ortam değişkeniyle bütün yazıcılar için seçiliyor; mutfak ve bar yazıcıları farklı markaysa biri bozuk basar. Yönetici yazıcıyı ekran üzerinden ekler, adres ve port girer, marka profilini yazıcı başına seçer.

## Owned surface

- `plan/v1/remediation/V1-RMD-589-printer-setup-and-profile.md`
- `evidence/V1-RMD-589/**`
- `database/migrations/V1/V1-RMD-589/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız yeni göç girişi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Kitchen/** - yalnız yazıcı varlığı, deposu ve taşıma katmanında profil
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/KitchenOperations/** - yalnız yazıcı oluşturma ve düzenleme ucu ve gönderimde profil
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/KitchenOperations/** - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Kitchen/** - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ApiConventions/idempotency_allowlist.json - yalnız yeni uç girişleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/** - yalnız yazıcı formu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız form bağlantısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni metin
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-581-kitchen-printer-encoding-and-status.md - yalnız Out of scope satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `kitchen.printers` tablosuna null olabilen profil alanı (Epson, Bixolon, Star, Ascii); boşsa ortam değişkeni geçerlidir.
- Yazıcı oluşturma (istemcinin ürettiği kimlikle) ve düzenleme: ad, istasyon, adres, port, profil, aktiflik; adres yalnız özel ağ IPv4 adresi (10/8, 172.16/12, 192.168/16) olabilir, ana makine adı, loopback ve herkese açık adres reddedilir; port 1-65535.
- Gönderimde yazıcının kendi profilinin kullanılması.
- Mutfak ekranında yönetim yetkisi olanlara yazıcı ekleme ve düzenleme formu.

## Out of scope

- Yazıcıya test fişi gönderme ve gerçek cihazla doğrulama (V0-PRN-001 Blocked).
- Yazıcı silme.

## Dependencies

- V1-RMD-581

## Acceptance evidence

1. Gerçek Postgres ve HTTP testleri: oluşturma, düzenleme, geçersiz adres/port/profil reddi, yetkisiz çağrı reddi, gönderimde profilin kullanılması.
2. Arayüz testleri, tür denetimi ve lint sıfır hata.
3. Mutasyon kanıtı.
4. Çıktılar `evidence/V1-RMD-589/` altındadır.

## Handoff

- None
