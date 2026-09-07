# V1.4 - QR and Online Ordering

## Hedef

QR, NFC ve Yemeksepeti siparişlerini güvenli biçimde ortak Order ve
PortionInventory domain'ine bağlamak.

## Giriş koşulu

`GATE-V14-ENTRY`, `plan/GATES.md`'de resmi olarak `GATE-V13-EXIT`
kapanmasına bağlıdır — V1.3 (cari hesap/dönemsel faturalama/QNB
e-fatura, 25 görev) bugün hâlâ tamamen `Planned`. Ayrıca bu README daha
önce, dış sözleşme sahipleri `V0-YSP-001`/`V0-QRG-001` açık `Blocked`
olduğu sürece hiçbir v1.4 modülünün ilerleyemeyeceğini yazıyordu — bu
yanlıştı: `qr-ordering`/`qr-security`/`qr-transport`'un `V0-YSP-001`
(Yemeksepeti) ile hiçbir bağı yok, yalnız `V0-QRG-001`'e bağımlılar.

Bugünkü gerçek durum:

- `V0-QRG-001` **2026-09-07'de `Done`** oldu (gerçek Cloudflare Tunnel
  kanıtı, `evidence/v0/integrations/V0-QRG-001/**`).
- `nfc-ordering`: `GATE-V13-EXIT`'e veya herhangi bir dış sözleşmeye hiç
  bağımlı değil (yalnız `Done` olan `V1-ORD-*`/`V1-TBL-*`, yerel ağ
  üzerinden çalışır) — istisna `V14-GOV-001` ile kaydedildi.
- `qr-ordering`/`qr-security`/`qr-transport` (ve bunlara bağımlı
  `customer-web`'in QR görevleri): artık yalnız `Done` olan
  `V0-QRG-001`'e bağımlı; `GATE-V13-EXIT`'i beklemeden ilerleyebilmeleri
  Semih'in açık iş kararıyla `V14-GOV-002`'de kaydedildi (`V14-QRS-001`'in
  `Dependencies`'inden `GATE-V14-ENTRY` çıkarıldı).
- `online-ordering`, `channel-mapping`, `reconciliation`, `reporting`,
  `shared-stock`: hâlâ `V0-YSP-001`'e (Yemeksepeti partner API, hâlâ
  `Blocked`) bağımlı **ve** hiçbir istisnaları yok — `GATE-V13-EXIT`
  kapanmadan ilerleyemezler.

## Çıkış kapısı

- Bu sürüm altındaki 20 görev dosyasının tamamı `Done`.
- Public token, replay, abuse ve rate-limit testleri geçer.
- PendingConfirmation masa davranışı kilitli ve concurrency-safe olur.
- Duplicate webhook duplicate order veya stok tüketimi üretmez.
- Restaurant/online son porsiyon yarışında yalnızca bir kanal kazanır.
- Catalog ve availability publish işlemleri provider sandbox'ında idempotent ve
  iç durumla mutabık kanıtlanır.
- QR customer web ve online operations UI yalnız sahip domain contract'larını
  çağırır; doğrudan state veya stok yazmaz.

## Modüller

`channel-mapping`, `customer-web`, `governance`, `nfc-ordering`,
`online-operations-ui`, `online-ordering`, `qr-ordering`, `qr-security`,
`qr-transport`, `reconciliation`, `reporting`, `shared-stock`.

Doğrulanan plan hacmi: 12 modül, 25 tek-sahip görev dosyası. 2026-09-07:
`V14-GOV-001` (NFC kanalının kabulü), `V14-GOV-002` (QR'ın `GATE-V13-EXIT`
beklemeden kabulü) ve `V14-QRT-002` (relay provider/onboarding kararı)
`Done`; `V14-NFC-001` (NFC self-check-in + güvenilir sipariş) ve
`V14-NFC-003` (anonim menü + sipariş sayfası) gerçek Docker test kanıtıyla
`Done`; `V14-NFC-002` (yaş kısıtlı ürün istisnası) `Planned`.
`qr-ordering`/`qr-security`/`qr-transport` artık yalnız `Done` olan
`V0-QRG-001`'e bağımlı, çalışmaya hazır. `online-ordering`/
`channel-mapping` hâlâ `V0-YSP-001`'e (Blocked) ve `GATE-V13-EXIT`'e tabi.
