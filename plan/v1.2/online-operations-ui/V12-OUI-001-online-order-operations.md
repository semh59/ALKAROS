# V12-OUI-001 - Build online order operations UI

- Task ID: V12-OUI-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Etki alanı komutlarını atlamadan QR bekleyen siparişler ve harici kanal siparişleri için yetkili personele bir
operasyonel kuyruk verin.

## Owned surface

- `src/Apps/BackOffice/OnlineOperations/**`, `tests/Apps/BackOffice/OnlineOperations/**`
- `src/Clients/PosTerminal/src/features/online-operations/**` — yol notu: depoda `src/Apps/BackOffice` uygulaması
  yok. Yetkili personelin kullandığı gerçek operasyon istemcisi PosTerminal olduğu için arayüz ve testleri buraya
  yazıldı.
- `src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs` — kuyruk okuma, kuryeye teslim ve iptal uçları;
  bu görevle oluşturulan yeni dosya.
- `evidence/V12-OUI-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncService.cs (V12-ONL-003 sahipliğinde) — teslim ve iptal
    komutlarına isteğe bağlı `expectedRowVersion`, `Stale` sonucu ve `NotAnOnlineOrderException`.
  - src/Host/DualScreen/DualScreenApplication.cs — yeni uçların kaydı.
  - src/Clients/PosTerminal/src/routes/workspace.tsx ve src/Clients/PosTerminal/src/strings.ts — gezinme öğesi ve
    rota.
  - tests/Host/Experience/OnlineOrdering/ (V12-ONL-001 sahipliğinde) — yeni OnlineOperationsHttpTests.cs,
    OnlineOrderingTestDatabase.cs tohum yardımcıları ve test csproj'una 148 numaralı migration fikstürü.
- Tasarım notu: kuyruk tek bir salt okuma sorgusudur. QR bekleyen siparişleri ve açık online siparişleri
  `orders` şemasından; son 24 saatin eşleme/aktarım sorunlarını ve yeniden deneme sayılarını ise V12-ONL-001..005
  tablolarından okur. Her eylem sahibi olan contract'a gider: QR onay/ret mevcut
  `/orders/{id}/accept|reject` uçlarına (V12-QRO-003), kuryeye teslim ve iptal ise V12-ONL-003 servisine. Ekran,
  gösterdiği siparişin `rowVersion` değerini gönderir; sürüm değiştiyse komut `409 CONCURRENCY_CONFLICT` ile
  reddedilir ve sipariş değişmez. Her eylemden sonra liste sunucudan yeniden okunur, yani ekran her zaman kalıcı
  sonucu gösterir. Yetki `orders.create`'tir. Sağlayıcı çağrısı V12-ONL-003'ün doğrulanmamış taslağıdır
  (V0-YSP-001 `Blocked`, `V12-GOV-004`).

## In scope

- Kuyruk filtreleri, kaynak/status görünürlüğü, kabul etme/reddetme/iptal etme eylemleri, eşleme hataları ve retry
  status.

## Out of scope

- Etki alanı geçişi uygulaması, kanal yapılandırması ve mutabakat çözümü.

## Dependencies

- V12-QRO-003
- V12-ONL-003
- V12-ONL-004
- V12-MAP-002
- V0-CMP-005

## Deliverables

- Rol korumalı işlemler arayüzü.
- Yetkilendirme, eşzamanlılık, eski komut ve hata sunumu testleri.

## Acceptance evidence

- Her kullanıcı eylemi, sahip olan contract modülünü çağırır ve kalıcı sonucunu gösterir; eski veya yetkisiz eylemler
  order'yi değiştiremez.
- Queue ve action akışları `docs/compliance/accessibility-target.md`'deki operations UI success kriterleri listesini
  karşılar.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): Host HTTP testleri 35/35 yeşil (6 tanesi yeni),
  PosTerminal testleri 212/212 yeşil (7 tanesi yeni). Sonuçlar:
  - Kuyruk bekleyen QR siparişlerini, açık online siparişleri ve eşleme sorunlarını gösterir; kaynak süzgeci
    sunucuda uygulanır.
  - Kuryeye teslim V12-ONL-003 servisinden geçer ve kalıcı sonucu gösterir.
  - Eski ekrandan gelen eylem `409` ile reddedilir; sipariş satırı değişmez.
  - İptal belgelenmiş bir gerekçe ister ve V12-STK-001 hold'unu serbest bırakır.
  - Oturumsuz ve yetkisiz çağıranlar hiçbir şeyi değiştiremez.
  - Online eylemler QR siparişini reddeder.
  - Arayüz yalnız Türkçe metin gösterir ve bilinmeyen sunucu değerleri "Diğer" olarak görünür.
  - Reddedilen eylemin nedeni yeniden yüklemeden sonra da ekranda kalır.
  - axe-core, açık iptal formu dahil kritik veya ciddi bulgu vermez.
  - Odak görünürlüğü ve 44 px dokunma hedefleri CSS'te tanımlıdır.
  - Ekran okuyuculu elle test (NVDA/VoiceOver) yapılmadı; yalnız otomatik denetim var. Açık madde (Semih
    2026-09-26, "Açık iş olarak kaydet"): bu ekranın NVDA/VoiceOver ile elle testi V20-UAT-001 kabul testi
    aşamasında yapılır; görev bu madde nedeniyle yeniden açılmaz.
- Mutasyon kontrolü (dosya yedekten geri yüklenip `cmp` ile doğrulandı): teslim gövdesinden `expectedRowVersion`
  çıkarılınca, bilinmeyen durum ham kodla gösterilince, sunucudaki sürüm kontrolü kapatılınca ve kaynak süzgeci
  yok sayılınca birer test kırmızıya döndü.
- Kanıt: `evidence/V12-OUI-001/`.

## Handoff

- V12-REC-001
- V15-REC-001
