# V1.2 - QR and Online Ordering

## Hedef

QR, NFC ve Yemeksepeti siparişlerini güvenli biçimde ortak Order ve
PortionInventory domain'ine bağlamak.

## Giriş koşulu

`GATE-V12-ENTRY`, `plan/GATES.md`'de resmi olarak yalnız `GATE-V11-EXIT`
kapanmasına bağlıdır (V1.1 zaten `Done`). 2026-09-07 sürüm yeniden
numaralandırmasıyla (`V12-GOV-003`) bu modül grubu V1.1'den hemen sonra,
V1.3 (cari hesap/fiscal/kasa) ve V1.4 (cari hesap/faturalama)'ı hiç
beklemeden ilerler — önceki numaralandırmada bu, `V12-GOV-002` ile
alınmış açık bir istisnaydı; artık numaralandırmanın kendisi bunu
yansıtıyor.

Bugünkü gerçek durum:

- `V0-QRG-001` **2026-09-07'de `Done`** oldu (gerçek Cloudflare Tunnel
  kanıtı, `evidence/v0/integrations/V0-QRG-001/**`).
- `nfc-ordering`: hiçbir dış sözleşmeye bağımlı değil (yalnız `Done`
  olan `V1-ORD-*`/`V1-TBL-*`, yerel ağ üzerinden çalışır) — kapsamı
  `V12-GOV-001` ile kaydedildi.
- `qr-ordering`/`qr-security`/`qr-transport` (ve bunlara bağımlı
  `customer-web`'in QR görevleri): yalnız `Done` olan `V0-QRG-001`'e
  bağımlı, çalışmaya hazır.
- `online-ordering`, `channel-mapping`, `reconciliation`, `reporting`,
  `shared-stock`: hâlâ `V0-YSP-001`'e (Yemeksepeti partner API, hâlâ
  `Blocked`) görev-seviyesinde bağımlı — versiyon giriş kapısı açık
  olsa da bu modüllerin kendi görevleri `V0-YSP-001` kapanmadan
  başlayamaz.

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
`V12-GOV-001` (NFC kanalının kabulü), `V12-GOV-002` (QR'ın eski
numaralandırmada `GATE-V13-EXIT` beklemeden kabulü, `V12-GOV-003` ile
artık yapısal hale geldi) ve `V12-QRT-002` (relay provider/onboarding
kararı) `Done`; `V12-NFC-001` (NFC self-check-in + güvenilir sipariş) ve
`V12-NFC-003` (anonim menü + sipariş sayfası) gerçek Docker test kanıtıyla
`Done`; `V12-NFC-002` (yaş kısıtlı ürün istisnası) `Planned`.
`qr-ordering`/`qr-security`/`qr-transport` artık yalnız `Done` olan
`V0-QRG-001`'e bağımlı, çalışmaya hazır. `online-ordering`/
`channel-mapping` hâlâ `V0-YSP-001`'e (Blocked) tabi.
