# V1-RMD-739 ilerleme

## Aşama 0 - Defter
`python tools/verification/ledger.py` -> `ledger.jsonl`: 1276 Done görev. Bayraklar: kanıt klasörü yok 558, günlük yok 395, boş kabul 9, commit yok 376.

## Aşama 1 - Tarama (`python tools/verification/scan.py`, sonuçlar `scans/`)
- Sahte/yarım kod (NotImplemented, TODO, FIXME, STUB): 0. Bastırılmış analizör: 0. Yorumlu kod: 0. Kayan noktalı para: 0.
- Yutulan istisna: 1 (vendor signalr.min.js, yanlış pozitif).
- Atlanan test: 1 (python testi, node yoksa atlıyor; gerekçeli).
- İşlem içi (InMemory) üretim sınıfları: InMemorySecretProvider, InMemorySecretRotationStore, InMemorySuspiciousLoginAuditSink (SecurityModule'de kayıtlı; Host AuditEventStoreSuspiciousLoginSink ile değiştiriyor mu, Aşama 2'de doğrulanacak: V15-SEC-002).
- Döngüde veritabanı çağrısı adayları: 14 (elle incelenecek, çoğu parametre döngüsü).
- Sınırsız sorgu adayı: 5 (çoğu alt sorgu; OnlineStoreStatusService.cs:174 küçük tablo).
- DateTime.Now/UtcNow kullanımı 346, istemcide ham `.message` kullanımı 146: Aşama 2'de görev kartlarında ele alınır.

## Aşama 2 - Görev kartları
Pilot: Payments dilimi (V1-RMD-604) tamam: 15 tamam, 17 düzeltilmeli, 1 yeniden aç.

## Aşama 2 durumu (oturum limiti nedeniyle durdu)
- Kart yöntemi: CARD_METHOD.md; gruplar batches.json (139 grup, her biri 9 görev; pilot 33 görev hariç).
- Biten gruplar: B001-B126 (B127 çalışıyor). Sonraki B128.
- Sonraki: B022'den itibaren aynı yöntemle devam; sonra %15 bağımsız kontrol, düzeltme görevleri, md güncelleme, Aşama 5.

## Aşama 2 TAMAM (2026-10-28)
139 grup, 1243 görev + pilot 33 = 1276. Karar sayıları (kartlardan, 1232 başlık ayrıştırıldı): TAMAM 907, DÜZELTİLMELİ 319, YENİDEN AÇ 6 (pilotta ayrıca 17 düzeltilmeli, 1 yeniden aç). Bulgular: Yüksek 45, Orta 300, Düşük 497 (`findings/register.md`, `decisions.json`).
Sonraki: Yüksek+YENİDEN AÇ bulguların bağımsız doğrulaması (qa-bulgu-dogrulayici), UI iki bakış (kullanıcı/tasarımcı), düzeltme görevleri, md güncelleme, Aşama 5.

## Yüksek bulgu doğrulaması ve UI incelemesi (2026-10-28)
- 45 Yüksek bulgu bağımsız doğrulandı (findings/verdict-1.md, verdict-2.md): 37 doğrulandı, 7 kısmen, 0 çürütüldü, 1 doğrulanamadı.
- UI iki bakış: kullanıcı gözü (ui/akis-uzmani, pazar-uzmani, eksik-avcisi), tasarımcı gözü (ui/gorsel-tutarlilik, erisilebilirlik-dokunma, durum-ve-hata, turkce-metin). Tamamı kod okuması.
- Sonraki: düzeltme planı (findings/fix-plan.md), PO soruları (findings/po-questions.md), md güncelleme, Aşama 5.
