# V1-RMD-656 kanıtı

- Test: `test-runs.log` (tests/Architecture, temiz derleme, 0 uyarı); yeni test `RawAuditInsertsStayOnTheKnownList`.
- Mutasyon: `mutations.log` 3/3 yakalandı (izin listesinden giriş çıkarma, başka dosyaya yeni ham INSERT, Audit modülünün dışlanmaması), dosyalar geri alındı (aynı).

## Plandan sapma (kararlaştırılan)

Görev metni Orders ve OnlineOrdering için Audit proje bağımlılığı ve üç ham INSERT'in ortak yazıcıya geçmesini öngörüyordu. Denendi: proje başvurusu eklenince NuGet kilit dosyaları (`packages.lock.json`) bu iki projeye bağımlı onlarca projede (Host, Billing, Kitchen, CustomerAccounts, testler...) tutarsız hale geliyor ve kilitli geri yükleme (NU1004) derlemeyi durduruyor. Başlamış görevin sahip yüzeyi genişletilemediğinden bu bağımlılık kenarı geri alındı (hiçbir dosya değişmeden bırakıldı).

Bunun yerine:
- Üç dosyanın yazdığı satırların şekli sabit ve doğrulandı: katalog neden kodu, kimlikler ve durumlar (kalem iptali/ikram), sağlayıcı ve alan adları (kimlik bilgileri; değer hiç yazılmaz), sağlayıcı ve durum (mağaza durumu). Serbest metin ve gizli değer yoktur; temizleyici ve doğrulama bu üçünde pratikte işlev görmezdi.
- `RawAuditInsertsStayOnTheKnownList` testi, Audit modülü dışında herhangi bir dosyada yeni bir ham `INSERT INTO audit.audit_events` çıkarsa derlemeyi kırar; izin listesi tam bu üç dosyadır. Host'taki iki nokta V1-RMD-655'te ortak yazıcıya geçmişti, bu testle başka ham yazım olmadığı kanıtlandı.
- Kalan risk: bu üç dosyaya ileride serbest metin alanı eklenirse temizleyiciden geçmez; test o değişikliği izin listesi üzerinden görünür kılar. İleride Audit bağımlılığı ve kilit dosyaları tek bir geniş kapsamlı görevle eklenebilir (ayrı iş, öncelik düşük).

## Ek düzeltme (V1-RMD-654'ün bıraktığı boşluk)

V1-RMD-654, `SentItemVoidStore`'un denetim mağazasını kullanmasını sağladı ama Host orkestratörlerinin onaylı bağımlılık listesine (`ApprovedHostOrchestrationEdges`) ve belgedeki satıra (`docs/architecture/module-dependency-rules.md`, `SentItemVoidStore`) `Audit` girdisini eklemedi; `HostOrchestrationEdgesStayWithinTheApprovedList` testi o görevin kapanışında koşulmamıştı. Bu görevde ikisi birlikte işlendi ve tests/Architecture paketinin tamamı koşuldu (`test-runs.log`).
