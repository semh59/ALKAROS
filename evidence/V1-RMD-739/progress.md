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
- Biten gruplar: B001-B015, B017 (kartlar tasks/ altında). B016, B018, B019, B020, B021 ajanları çalışırken oturum limiti doldu; kartları tasks/ altında yazılmışsa tamam, yoksa yeniden koşulmalı.
- Sonraki: B022'den itibaren aynı yöntemle devam; sonra %15 bağımsız kontrol, düzeltme görevleri, md güncelleme, Aşama 5.
