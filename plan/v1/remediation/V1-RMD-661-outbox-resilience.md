# V1-RMD-661 - Olay kuyruğu dayanıklılığı (zaman aşımı, kira bırakma, kapatma, hata nedeni, eski kayıt temizliği, kesinti bekleme süresi)

- Task ID: V1-RMD-661
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

V1-RMD-596 bulguları B003, B005, B006, B008, B009, B017 ve B018 kapanır. B003/B017: tüketici çağrısı zaman aşımına uğrarsa (TaskCanceledException) bu bir başarısızlık sayılır, deneme sayacı artar, parti takılmaz; her tüketici çağrısına üst süre konur. B008: süresi dolan kira bırakılırken deneme sayacı artar ve bütçe dolarsa mesaj ölü olur (süreci çökerten zehirli mesaj sonsuza kadar dönmez). B009: kapatma sırasında teslim edilmiş mesaj `dispatched` yazılır (çift teslim yok) ve işlenmemiş mesajlar 5 dk beklemeden hemen `pending` olur. B006: hata nedeni `last_error` alanına (yalnız istisna türü adı, sır içermez) ve loga yazılır. B005: teslim edilmiş kayıtlar 14 gün, ölü kayıtlar 90 gün sonra silinir. B018: veritabanı kesintisinde dispatcher bekleme süresi 1 sn'den 30 sn'ye üstel artar.

## Owned surface

- `plan/v1/remediation/V1-RMD-661-outbox-resilience.md`
- `evidence/V1-RMD-661/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Messaging/OutboxStore.cs - yalnız dağıtım, kira bırakma, temizlik
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Messaging/RetryPolicy.cs - yalnız hata metni
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Outbox/OutboxDispatcherHostedService.cs - yalnız bekleme süresi ve temizlik çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Outbox/OutboxComposition.cs - yalnız günlükçü aktarımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Messaging/OutboxStoreTests.cs - yalnız beklenti güncellemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Messaging/RetryPolicyTests.cs - yalnız beklenti güncellemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Messaging/OutboxResilienceTests.cs - yalnız yeni test dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-660-unit-converter-correctness.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Tüketici zaman aşımı sayımı ve üst süresi, kira bırakmada deneme artışı ve ölüme geçiş, kapatmada kayıpsız sonlandırma, hata türü adının saklanması ve loglanması, temizlik (silme) işlemi, kesinti bekleme süresi ve bunların testleri.

## Out of scope

- Profilsiz olay bütçesinin yükseltilmesi (B007), ölü mektup izleyicisinin açılıştaki ölüleri bildirmesi (B016), olay sırası sözleşmesi (B021), ölü `inbox_messages` şeması (B024): sonraki göreve ayrılmıştır.
- Ölü mektup yönetim ekranı (Fırsat 596-F001).

## Dependencies

- V1-RMD-660

## Acceptance evidence

1. Önce kırmızı test (zaman aşımı sayılır, takılan tüketici sınırlanır, kira bırakma sayacı artırır ve ölüme geçirir, iptalde teslim edilmiş mesaj dispatched yazılır ve işlenmemişler pending olur, last_error tür adı içerir, temizlik süreleri, bekleme süresi artışı), sonra yeşil; mutasyon kanıtı.
2. `evidence/V1-RMD-661/` altında çıktılar; Messaging ve OutboxStore kullanan test projeleri yeşil.

## Handoff

- V1-RMD-662
