# V1-RMD-581 - Mutfak yazıcısı: Türkçe karakter tablosu, ibare ve durum okuma

- Task ID: V1-RMD-581
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Dış kaynak araştırmasında (Epson, Bixolon, Star kılavuzları; GİB GMP-3 v5.0 madde 15) kodda üç eksik bulundu:
1. Fiş metni UTF-8 gönderiliyor, yazıcıya karakter tablosu seçtirilmiyor; ürün adı, ek ve notlardaki ç ğ ı ö ş ü bozuk basılır.
2. Yazıcıdan durum okunmuyor; kağıt bitmesi, kapak açık veya hata, TCP yazımı başarılı göründüğü için fark edilmez.
3. Mutfak fişinde "MALİ DEĞERİ YOKTUR- FİŞ YERİNE GEÇMEZ" ibaresi yok.

## Owned surface

- `plan/v1/remediation/V1-RMD-581-kitchen-printer-encoding-and-status.md`
- `evidence/V1-RMD-581/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Kitchen/PrintQueue/** - yalnız karakter tablosu profili, ibare ve durum okuma
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Kitchen/PrintQueue/** - yalnız ilgili testler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yazıcı profili (Epson, Bixolon, Star, Ascii) ve profile uygun Türkçe karakter tablosu seçimi; profil `ALKAROS_PRINTER_PROFILE` ile seçilir, varsayılan Epson.
- Yazımdan sonra `DLE EOT 1` ve gerekirse `DLE EOT 2` ile durum okuma; kağıt bitmesi, kapak açık ve hata ayrı mesajla "teslim belirsiz" sayılır; yanıt gelmezse eski davranış sürer.
- Fişe ibare satırı.

## Out of scope

- Yazıcı başına profil (veritabanı alanı ve ekran), gerçek cihazla doğrulama (V0-PRN-001 Blocked).

## Dependencies

- V1-RMD-130

## Acceptance evidence

1. Sahte TCP yazıcıyla baytlar tablo seçimi ve doğru kod sayfasıyla gelir; durum yanıtlarına göre özel durumlar; mutasyon kanıtı `evidence/V1-RMD-581/` altındadır.
2. Cihazla doğrulanmadığı README'de açıkça yazılır.

## Handoff

- None
