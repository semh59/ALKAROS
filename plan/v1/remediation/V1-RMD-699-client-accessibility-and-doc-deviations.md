# V1-RMD-699 - İstemci erişilebilirlik ve doküman sapmaları

- Task ID: V1-RMD-699
- Status: Done
- Assignee: claude-code-session_01CEenPfDb6pEWVcDd9W7VdF
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-599 bulguları B068, B122, B123, B127, B128, B131, B133, B135, B137, B139, B140, B141, B143 kapanır. Bekleyen hesap kasada ses ve rozetle belirir; dokunma hedefleri 48 piksel alt sınırına uyar ve arka perde erişilebilirlik uyarıları kalkar; sabit bekleme kullanan testler olay bekler; Host belgeleri (topoloji, usage metni, kapı kontrol listesi, katalog izin kodları, order.json aralığı) koda uydurulur; yanlış yorumlar düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-699-client-accessibility-and-doc-deviations.md`
- `evidence/V1-RMD-699/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/** - yalnız bekleyen hesaplar ve dokunma hedefleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/** - yalnız Host belgeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/** - yalnız Host belgeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/data/** - yalnız Host belgeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız phaseBRange alanı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız usage metni ve yorumlar
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/** - yalnız yorum düzeltmeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/** - yalnız izin kodu kataloğu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/** - yalnız bekleme düzeltmeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/** - yalnız bekleme düzeltmeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Clients/** - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/README.md - yalnız bağlayıcı ağ açıklaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/Dockerfile - yalnız bağlayıcı ağ yorumu
- `src/Clients/PosTerminal/src/touchTargets.test.ts`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-698-migration-line-endings-and-indexes.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kasa bekleyen hesap bildirimi, erişilebilirlik, test bekleme düzeltmeleri, belge ve yorum düzeltmeleri.

## Out of scope

- Table-draft siparişleri müşteri ekranında (Semih sorusu S9); bilinen B133/B140 çözümleri başka görevdedir.

## Dependencies

- V1-RMD-698

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-699/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-600
