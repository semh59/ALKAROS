# V1-RMD-724 - Orders doküman sapmaları, down betikleri ve sınır istisnası

**Sonuç:** B008, B022, B023, B030 kapandı; B031'in doküman kalemi (KVKK notu) kapandı, kod ve runbook kalemleri V1-RMD-725'e taşındı.

- B023: yeni migration 206 (`database/migrations/V1/V1-RMD-724`). Geri alırken 105 ve 077 down betiklerinden önce Nfc siparişi Qr'a, Held kalemi Sent'e çevirir. Gerçek psql ile Nfc ve Held satırı varken 206, 105, 077 down sırası çalışır (`logs/rollback-manifest.log`). Geri almada Nfc/Held ayrımı kaybolur; README ve migration yorumunda yazılı.
- B022: `consistency_audit.py` artık Host'un `table_mgmt` yazısına yalnız `tables` için izin verir; `table_merges` (OrderSettlementService) ve `table_sent_checks` (CashierHandoffStore) gerekçeli tek tek istisna. Başka bir `table_mgmt` tablosuna yazı yeni istisna olmadan hata verir.
- B008, B030: kanonik katalog (Nfc, Held), sipariş numarası biçimi, OrderStateChanged "planlı", Measurements, NfcOrdering satırı, api-contract örneği notu, void politikası hizalama bölümü, `bill_items` adı, NFC yaş kısıtı metni koda hizalandı. Karar değişikliği yok; PO sorusu: bölüm "Documentation alignment" içindeki ilk madde.
- KVKK: void/ikram gerekçesi `order_status_history.reason` anonimleştirme listesinde yok; runbook'ta bilinen açık olarak yazıldı, temizleme üretim değişikliği olarak 725 dışı bırakıldı (kod görevi gerekir).

## B031 sınıflaması
Alt kalemler ajan tablolarının ham metnini içermiyor (yalnız özet satırı); kod kalemleri 725'te koda bakılarak yeniden doğrulanıp düzeltilir veya gerekçeyle kapatılır. Burada kapanan: KVKK notu. 725'e taşınan: diğer tüm kalemler (kod, istemci ve operasyon runbook).

## Kanıt
- Mutasyon: `mutations.log` - 5/5 KILLED.
- Testler: `logs/` (geri alma + manifest 18/18, Host migration grubu 48/48 `host-migrations.log`, 202 geri alma testi düzeltildi `terminal-key.log` (203-205 sonradan eklenince ön koşulu bozulmuştu)).
