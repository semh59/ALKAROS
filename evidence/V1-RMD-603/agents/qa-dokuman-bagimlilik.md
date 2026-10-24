# qa-dokuman-bagimlilik - V1-RMD-603 Billing (SHA feb38368)

Kontrol: 120/120 dilim dosyası (2 otomatik tarama + elle okuma); 14/14 doküman; doküman atıflarındaki tüm yollar sınandı (eksik yok); 17/17 Billing ilişkili Done görevi; csproj 2+4, packages.lock.json 5/5, Directory.Packages.props (10 paket), PosTerminal pnpm-lock/package.json, Cashier/WaiterPwa E2E. Lisans: PosTerminal .pnpm 112 paket (MIT 92, Apache-2.0 5, BSD-2 3, BSD-3 3, ISC 4, MIT-0 1, MPL-2.0 3, Unlicense 1), E2E 16'şar paket; GPL/AGPL/SSPL/lisanssız yok (yalnız bu makinede kurulu paketler). Betikler tmp/qa/V1-RMD-603/qa-dokuman-bagimlilik/.

| No | Önem | Kanıt | Sorun | Etki |
|---|---|---|---|---|
| D1 | Düşük | split-payment.js:42; BillingSplitStore.cs:64; ALKAROS.Billing.csproj:15; dilimde 40 dosyada 234 satır görev kimliği/tarih yorumu (168 src, 66 test) | Yorumlar görev kimliği, denetim tarihi, tarihçe taşıyor (kullanıcı kuralına aykırı). En yoğun: split-payment.js 36, BillingSplitHttpTests 20, bill.js 20, PaymentTenderHttpTests 17, cash-session.js 17, BillingSplitStore 16. | Yorum gürültüsü |
| D2 | Düşük | projection-ownership.md:21; grep BillLineChanged = 0 | Doküman sapması: BillLineChanged olayı kodda yok. | Olmayan olay vaadi |
| D3 | Düşük | module-dependency-rules.md:50 (Order satırı :49 "planned" işaretli) | Doküman sapması: BillStateChanged yayımlanıyor gibi yazılı, kodda yok; işaret tutarsız. | Yanıltıcı |
| D4 | Orta | api-contract-standard.md:123 vs BillingSplitApplication.cs:31, DualScreenApplication.Payments.cs:151, BillingSplitContracts.cs:108,134 | Doküman sapması: Örnek 2 yolu (/api/v1/bills/{billId}/payments), başlıkla idempotency ve hata kodları (TENDER_OVER_ALLOCATION, CONCURRENT_MODIFICATION) gerçekle uyumsuz; gerçek: terminalli /tenders, gövde anahtarı, *_REUSED kodları. Örnek 1'deki not Örnek 2'de yok. | Örnekten yazılan istemci çalışmaz |
| D5 | Düşük | void-complimentary-discount-policy.md:36,37,49 vs AdjustmentCalculator.cs:20-66; grep LineType.(Discount\|Refund\|Adjustment) = 0 | Doküman sapması: politika "reddedildi" dediği fatura seviyesi iskonto kodun tek yolu; BillLineType.Discount/Refund/Adjustment tanımlı ama üretilmiyor. PO kararı gerekir. | Politika-kod çelişkisi |
| D6 | Düşük | remaining-gaps-register.md:22,43,46 vs BillingSplitApplication.cs:415, split-payment.js:406,469 | Kayıt 2026-09-25; kalem bazlı bölme ve istemci sonradan kapanmış, kayıt açık gösteriyor. PartiallyPaid geçişi doğrulanmadı. | Bayat kayıt |
| D7 | Düşük | migration-dependency-graph.md:71-74,150-151 | bill_adjustments, bölme tasarımı tabloları, iptal alanları grafta yok. | Eksik graf (tarihsel olabilir) |
| D8 | Düşük | ALKAROS.Billing.csproj:9-13; Orders.csproj:21; 5 dosyada using Npgsql | Billing Npgsql'i kendi PackageReference'ı olmadan Orders üzerinden geçişli alıyor. | Orders bırakırsa Billing derlenmez |
| D9 | Düşük | SKILL alkaros-api-endpoint:14 vs BillingSplitApplication.cs:596 | SKILL "IDEMPOTENCY_KEY_CONFLICT" diyor, Billing IDEMPOTENCY_KEY_REUSED kullanıyor. | Yanlış kod seçimi riski |
| D10 | Düşük | evidence/V1-RMD-410/tests.log | exit code satırı yok. | Zayıf kanıt |

Bulgusuz: Evidence'ı olmayan Done görevler (V1-BIL-005, RMD-103/112/191/276/292/298, WTR-020) kural tarihinden (2026-09-28) önce -> tarihsel; Owned surface yolları tarandı; Directory.Packages.props 10 paket kullanılıyor, FluentAssertions 6.12.0 politikaya uygun; lisans politikası var (third-party-license-policy.md).

Temiz: BillState ve line_type enum/CHECK/kod birebir; Host Billing kenarları dokümanla aynı; ProjectReference'lar bağımlılık dokümanıyla uyumlu; merkezi sürüm + RestoreLockedMode; TODO/NotImplemented/boş catch/pragma/Skip = 0.

Doğrulanamayanlar: Logging.Abstractions kilit requested 8.0.3/resolved 8.0.2 nedeni; NuGet geçişli paket lisansları (4 paket, V1-RMD-596 bilinen); idempotency istisna listesi araması zaman aşımı (altyapı); PartiallyPaid geçişi; NuGet CVE taraması yapılmadı. PaymentTender.log exit=1 (1 kırmızı test) ana oturuma not.
