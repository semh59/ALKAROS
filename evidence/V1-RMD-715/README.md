# V1-RMD-715 - Masa doküman sapmaları ve modül sınırı düzeltmeleri

Kapanan V1-RMD-601 bulguları: B032 (D1, D3, D4, D7–D14), S4, S5, S6, S7.

## Değişiklikler
- Dokümanlar koda göre düzeltildi: masa geçiş matrisi (`lifecycle-transition-contracts.md`), işaretçi projeksiyonu sahipliği (`TableStateChanged` olayı yok, `projection-ownership.md`), masa tekilliği ve iki kısmi benzersiz indeks (`nullable-unique-policy.md`), Tables'ın `X-Idempotency-Key` sapması ve gerçek UUID örneği (`api-contract-standard.md`), rezervasyon bitiş zamanı ve otomatik süre dolumu (`table-reservation-policy.md`, V12-QRO-003 artık Done), `table_number` metin ve bölge başına tekil (`single-branch-key-strategy.md`), tarihsel not (`check-and-table.md`), Tables'ın orders/billing/payments okumaları ve kendi audit INSERT'leri (`module-dependency-rules.md`).
- Kod yorumu: `TableState` artık Reserved'ın elle rezervasyonla da girildiğini söyler; `TablesModule` kayıt bölünmesinin nedenini yazar (S5, davranış değişmedi).
- S4/S7: Tables'ın 5 dosyadaki 8 ham audit INSERT'i "tablo adı sabitin arkasında" olduğu için ham-INSERT testine görünmüyordu. INSERT'ler düz yazıya çevrildi, dosyalar bilinen listeye ve gerekçesi yazıldı; test artık audit adlı sabitin arkasındaki INSERT'i de yakalar (kalıp için 6 satırlık teori testi).

## Karar: neden `AuditEventWriter`'a taşınmadı
Tables → Audit proje başvurusu denendi. Kilit dosyası geri yüklemesi (`--force-evaluate`) 120 `packages.lock.json` dosyasını (Orders, Billing, Payments, QrOrdering ve ~100 test projesi) değiştirdi. Yarım düzine alan için bu kadar yayılma kabul edilmedi ve geri alındı. Orders ve OnlineOrdering için de aynı gerekçeyle ham INSERT'e izin verilmişti (test listesindeki yorum). Satırlar yalnız kimlik, durum, sürüm ve personelin yazdığı gerekçeyi içerir.

## Test kanıtı (tümü exit 0)
ModuleBoundaries 17 (6 yeni teori satırı dahil), TableMerge, TableTransfer, Reservations, CurrentPointers, Host Tables. Günlükler `logs/`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
5 mutantın 5'i KILLED, kaynak geri yüklendi ve doğrulandı (A3 betikteki kaçış hatası düzeltilip yeniden koşuldu).

## Doğrulanamayan / açık kalan
- Doküman düzeltmelerinin doğruluğunu otomatik denetleyen test yok; kodla elle karşılaştırıldı.
- NetArchTest.Rules lisansı (D16) çevrimdışı doğrulanamadı.
- `alkaros-api-endpoint` SKILL'i repo dışında (kullanıcı dizini) olduğundan Tables sapması orada işlenmedi (D7'nin SKILL kısmı).
- D15 (V1-TBL-001 kanıtındaki eski test adları) tarihsel kayıt olduğu için dokunulmadı.
- S5: DI birleştirilmedi, yalnız nedeni yazıldı.
