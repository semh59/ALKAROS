# V1-RMD-711 - Masa olayları, transfer ve durum ucu güvenliği

Kapanan V1-RMD-601 bulguları: B005 (S1, S3), B006, B007 (transfer, durum, masa düzenleme), B021, B022, B024 (eşleme).

## Değişiklikler
- Birleşim ve transfer olayları taşınacak sipariş ve hesap kimliklerini taşır; tüketiciler (Orders, Billing) yalnız o kimlikleri ve yalnız hâlâ beklenen masada duranları taşır. Birleşim olayı yalnız birleşim hâlâ aktifken uygulanır: geç/yeniden gelen eski olay ayrılmış birleşimi geri kurmaz. Kimlik listesi olmayan eski olaylar önceki davranışa düşer.
- Transfer, aktif birleşimdeki masayı reddeder (`TABLE_IN_MERGE`).
- Durum ucu: Dolu masayı elle boşaltma, açık (taslak olmayan) siparişi veya ödenmemiş hesabı olan (`TABLE_HAS_OPEN_WORK`) veya birleşimdeki (`TABLE_IN_MERGE`) masada reddedilir. Masa düzenleme dolu/rezerve masayı pasife almayı reddeder (`TABLE_BUSY`).
- Denetim kaydı: bölge oluştur/güncelle/sil, masa oluştur/güncelle/durum, kat planı kaydı (`IAuditEventStore`, Host→Audit kenarı testte ve dokümanda).
- `status_changed_at`: migration 204 tetikleyicisi, durum değiştiren her yazımda (sipariş akışları dahil) günceller.
- 40001/40P01 → 409 çakışma, 22001 → 400.

## Test kanıtı (gerçek PostgreSQL, tümü exit 0)
TableMerge 37, TableTransfer 38, Host Tables 22 (6 yeni: TableGuardsHttpTests), CurrentPointers 19, Reservations 27, TableLifecycle 59, PaymentTopology 11, manifest 17, ModuleBoundaries 11. Günlükler `logs/`. Migration 204 ileri/ikinci ileri/geri: `logs/migration-up-down.log`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
N1–N15 hepsi KILLED, kaynaklar geri yüklendi ve doğrulandı. N13 ilk koşuda SURVIVED (sütun listeli tetikleyici zaten yalnız durum yazımında ateşlenir); "aynı durumu yazmak değişiklik değildir" testi eklendi, yeniden koşuda KILLED (logda iki N13 satırı).

## Doğrulanamayan / açık kalan
- 40001/40P01/22001 hata eşlemesi için test yok (üretmesi zor); kod okuması.
- Olay kimliğiyle tekilleştirme (inbox), ölü olay uyarısı/yeniden oynatma, işaretçi onarım işi, B025 ve B030: V1-RMD-714'e alındı.
- Transfer gidip gelme sonrası aynı olayın yeniden teslimi (kimliklerin başka masaya geri taşındığı durum) tekilleştirme olmadan korunmaz; sıklığı çok düşük.
