# V1-RMD-788 - Sonuç

## Değişiklik
- Kasa giriş/çıkışında açıklama zorunlu (1-500 karakter, kırpılır): alan kuralı + veritabanı CHECK (NOT VALID; eski satırlar okunur kalır, `CashTransaction.Restore`).
- Kasadaki nakitten fazla çıkış yok: bakiye kontrolü ve ekleme tek işlemde, oturum satırı `FOR UPDATE` kilidi altında.
- Eşik üstü (kesin büyük) çıkış `cash.cash_movement_requests` kaydı olur (202); `cash.session.override` yetkili ve talep sahibi olmayan kişi onaylar/reddeder, talep sahibi iptal eder. Onayda bakiye yeniden kontrol edilir.
- Yönetici kendi talebini yalnız `approvals.manager_self_approval_enabled` açıkken onaylar; kayıt `self_approved=true` işaretlenir (varsayılan kapalı, tek yerde tanımlı; G4 iadeleri aynı ayarı kullanır).
- Eşik ayarı `cash.cash_out_approval_threshold` (0-1.000.000 TL, varsayılan 500). Ayar değişiklikleri/pasife alma audit'e düşer (migration 212 tetikleyicisi, değişiklikle aynı işlemde).
- Bekleyen talep varken sayım başlatma/kapanış reddedilir; aynı anahtar tekrarında yön+tutar+açıklama karşılaştırılır (uyumsuz: 409), karara bağlanmış talep 200 döner.
- Hareket listesi `GET .../cash-movements` (en yeni 200, `movementsTruncated`).
- Satır 12 ve 7 (V1-RMD-787) uyumluluğu: yeni akışta bozulmadı, mevcut testler geçiyor.

## Doğrulama
- Kırmızı: `red-host` 19 başarısız / 0 geçen. Yeşil: CashSession 72/72 (`k1`,`k2`,`k3`), Settings 65, Errors 217, Ledger 46, CashReceipts 11, Manifest 17, ApiConventions 17, ModuleBoundaries 17 (`l-*`).
- Migration 211/212 ileri, geri, tekrar ileri ve ikinci uygulama (idempotent): `migration-forward-backward.log`.
- Mutasyon: 16 mutant (m1-m10, m12-m17) hepsi öldü, dosyalar `cmp` ile birebir geri alındı (`mutation-summary.txt`). m11 (uç nokta audit'i) tetikleyici yaklaşımına geçince m16 (WHEN koşulu silinir) ile değiştirildi. Kilitsiz mutant m14 eşzamanlılık testlerince yakalandı.
- plan-audit 0/0, consistency-audit clean, task-scope exit 0.

## İncelemeler (sırayla)
1. qa-backend: 8 bulgu (kapanış yarışı, audit atomikliği, kilit altında ikinci bağlantı, migration idempotensi, ret notu sınırı, kesilme işareti, tekrar yanıtı, pasif eşik) - hepsi giderildi veya belgeli.
2. qa-test-kalitesi: 21 bulgu - hepsi giderildi (çapraz oturum, eşik+kasa üstü, self_approved, DB kısıtları, paralel anahtar, ayar doğrulama, audit içeriği ve atomiklik, liste alanları, 200/201 sınırı, tekrar kombinasyonları, eşzamanlılık, kilitsiz mutant, tetikleyici testleri).
- Not: Ayar tablosundaki `data_type` değerleri 'Integer'/'Boolean' CHECK kısıtının izin verdiği değerlerdir (API'de WholeNumber/Toggle görünür); hata değil.

## Açık risk / ayrı karar
- Talep, sayım başladığı anda commit edilirse atıl kalabilir; kapanış engellenir, iptal edilebilir (test: `ARequestCommittedInTheInstantTheCountStarted...`).
- `Refund` defter satırları oturum kilidini almaz: G4 grubunda ele alınacak.
- F0409 (nakit TenderedAmount tekrarı) G4 ödeme grubuna ait, kapsam dışı.
- Ekran 789'da (arayüz).
