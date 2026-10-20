# Test ve Kalite Programı Yürütme Kılavuzu

Kapsam: V1-RMD-595 ile V1-RMD-638 arası 44 görev. Her görev salt okunur bir denetimdir; kod değiştirmez. Doğrulanan bulgular ayrı düzeltme görevlerine (V1-RMD-639 ve sonrası) dönüşür.

## 1. Sıra

| Faz | Görevler | İçerik |
| --- | --- | --- |
| Pilot | 595 | Audit. Gerçek token ve süre burada ölçülür, bu kılavuz düzeltilir. |
| Temel | 596-599 | BuildingBlocks, Identity, Settings, Host |
| Para zinciri | 600-610 | Catalog, Tables, Orders, Billing, Payments, Cash, CustomerData, CustomerAccounts, Invoicing, Reconciliation, Reporting |
| Hukuk ve güvenlik | 611-615 | Privacy, Security, Observability, Operations, Support |
| Kanallar | 616-618 | Kitchen, QrOrdering ve QrRelay, OnlineOrdering |
| Stok ve maliyet | 619-623 | Inventory, Recipes, Production, Purchasing, Menu |
| İstemciler | 624-627 | PosTerminal, Cashier, WaiterPwa platformları, misafir yüzleri |
| Akışlar | 628-637 | Modüller arası uçtan uca 10 akış |
| Kapanış | 638 | Kapsam birleşimi ve kayıt belgesi |

Her görev bir öncekine bağlıdır. Sıradaki görev, öncekinin Kritik ve Yüksek bulguları düzeltilmeden başlamaz (bkz. §7).

## 2. Dilim kuralı

Bir denetim dilimi şunlardan oluşur; hepsi görev metninde açık yol listesi olarak yazılır (ajanın varsayılan desenine güvenilmez):
- modül klasörü (`src/Modules/<M>/**`),
- o modülün Host dosyaları (uçlar modül klasöründe değil `src/Host/**` altındadır),
- şemasını anan göçler ve izin tohumu göçleri,
- `tests/Modules/<M>/**` ve `tests/Host/**` içindeki ilgili testler,
- ilgili E2E spec'leri ve istemci ekranları.

Başlangıçta dilimin dosya listesi `evidence/<Görev>/baseline/scope-files.txt` dosyasına yazılır. Paylaşılan dosyalar (ör. `DualScreenApplication.Endpoints.cs`, `SecurityAdministrationEndpoints.cs`) rota bazında bölünür ve görev metninde hangi rotanın kime ait olduğu belirtilir.

## 3. Ajan kadrosu

Tanımlar `~/.claude/agents/` altındadır. Ortak kurallar E1-E14 her ajan dosyasının sonunda yazılıdır.

| Ajan | Odak |
| --- | --- |
| qa-backend | iş mantığı, para, durum geçişi, eşzamanlılık |
| qa-api-uclar | uç sözleşmesi, yetki, doğrulama, uç başına test |
| qa-veritabani | göç, şema, indeks, sorgu, sınırsız büyüme |
| qa-sinir-mimari | modül sınırı, bağımlılık, olay ve kayıt eksikleri |
| qa-frontend | ekran durumları, sunucu uyumu, tsc ve oxlint |
| qa-ux-tasarim | stil rehberi, Türkçe metin, rol akışı, öğrenme eşiği |
| qa-guvenlik-yetki | yetki matrisi, OWASP, KVKK, sır sızıntısı |
| qa-test-kalitesi | test gücü, mutasyon ölçümü |
| qa-performans-dayaniklilik | yük, arıza davranışı, kaynak |
| qa-operasyon-gozlemlenebilirlik | dağıtım, log, alarm, yedek |
| qa-mevzuat-uyum | KDV, e-fatura, ÖKC, KVKK kuralları (yalnız repodaki kaynak) |
| qa-entegrasyon-cihaz | QNB, Yemeksepeti, Trendyol Go, Token, yazıcı, çevrimdışı |
| qa-dokuman-bagimlilik | doküman-kod tutarlılığı, bağımlılık, lisans |
| qa-uctan-uca-akis | modüller arası akış (yalnız akış görevleri) |
| qa-bulgu-dogrulayici | karşı denetçi; her bulguyu çürütmeye çalışır |
| ui-* (7 ajan) | yalnız istemci ve akış görevleri |

Görev metni her ajan için "tam" veya "dar kapsamlı" koşuyu belirtir.

## 4. Ajan istem şablonu

```
GÖREV: V1-RMD-<no> <modül>. SHA: <commit>. Rolün: <ajan>.
Dilim dosya listesi: evidence/V1-RMD-<no>/baseline/scope-files.txt (yol listesi aşağıda).
Yazabileceğin tek yer: tmp/qa/V1-RMD-<no>/<ajan>/.
Derleme ve test kayıtları: evidence/V1-RMD-<no>/runs/ (okuyabilirsin, yeniden koşma; yalnız qa-test-kalitesi koşar).
Beklenen çıktı: rolündeki rapor biçimi; "N/M kalem kontrol edildi" satırı M=<başlangıç sayımı>.
Bilinen kayıtlar (aynen "bilinen" işaretle): <ilgili eski görev ve boşluk kaydı kimlikleri>.
```

Ana oturum her ajanı tek tek, arka planda çalıştırır; ajanlar alt ajan çağıramaz.

## 5. Kanıt düzeni (`evidence/<Görev>/`)

| Klasör veya dosya | İçerik |
| --- | --- |
| `baseline/` | `git rev-parse HEAD`, başlangıç `git status`, `scope-files.txt`, sayım tablosu ve üreten komutlar, ajan tanımlarının sha256 değerleri, dilime düşen idempotency ve API istisnaları, dilime düşen eski bulgular |
| `runs/` | derleme ve test kayıtları (exit code ile); istemci için tsc, oxlint, vitest |
| `agents/<ajan>.md` | ham rapor, sırlardan arındırılmış |
| `verifier/` | doğrulayıcı çıktısı (aynı SHA) |
| `checks/` | her `dosya:satır` alıntısının o SHA'da var olduğunu gösteren betik çıktısı; bulgu sayım tablosu |
| `findings.json` | bulgu kimliği, önem, kanıt düzeyi, durum, düzeltme görevi |
| `mutation/` | yalnız qa-test-kalitesi yazar |
| `README.md` | özet: sayılar, temiz alanlar, doğrulanamayanlar, Fırsat tablosu, Semih metni, senaryo |
| `closing/` | plan_audit, consistency_audit ve task_scope çıktıları |

Evidence'a sır veya kişisel veri girmez; commit öncesi sır deseni taraması yapılır; `.e2e-state.json` ve ham log kopyalanmaz.

## 6. Bulgu kuralları

- Kimlik: bulgu `<Görev>-B<nnn>`, fırsat `<Görev>-F<nnn>` (ör. `V1-RMD-603-B004`).
- Kanıt düzeyi: Çalıştırıldı, Kod okuması, Çıkarım. Çıkarım Kritik veya Yüksek olamaz.
- Durum: DOĞRULANDI, KISMEN, ÇÜRÜTÜLDÜ, DOĞRULANAMADI. Yalnız doğrulananlar düzeltme görevi olur.
- Doğrulama: `qa-bulgu-dogrulayici` her bulguyu aynı SHA üzerinde sınar (Kritik ve Yüksek tek tek, diğerleri en çok 15'lik partiler). Ana oturum Kritik ve Yüksek bulguları ayrıca kodda yeniden açar.
- "Ön tarama" ve eski not iddiaları girdidir; doğrulanmadan bulgu sayılmaz.
- Ajan başına çürütme oranı izlenir; oran yüksekse o ajan yeniden koşturulur veya daraltılır.
- Dış servis, cihaz veya altyapı yüzünden doğrulanamayan bulgu "doğrulanamadı (neden)" yazılır ve mevcut dış görevlere bağlanır (V0-QNB-001, V0-YSP-001, V0-PRN-001, V0-QRG-001, V0-HUG-001, V1-RMD-572).
- Doküman-kod çelişkisi kusur değil "Doküman sapması" olarak tek satır yazılır; kod esastır. Sapmalar her faz sonunda tek dokümantasyon görevinde toplanır.
- Önem ölçeği: Kritik = para veya veri kaybı, güvenlik, mevzuat ihlali, servis durması; Yüksek = ana akışı bozar, geçici yol yok; Orta = geçici yol var; Düşük = kozmetik.

## 7. Düzeltme politikası

- Önem yalnız düzeltme sırasını belirler; doğrulanan her bulgu düzeltilir.
- Kritik ve Yüksek bulgular bir sonraki denetim görevi başlamadan düzeltilir. Orta ve Düşük bulgular fazın sonunda toplu düzeltilir.
- Her kök neden ayrı düzeltme görevidir. Görev önce kırmızıya dönen test, sonra düzeltme ve doğrulama kanıtı ister.
- Fırsatlar (çalışanın işini azaltma, hız, öğrenme eşiği) Semih'in ürün kararına gider.
- Ürün, hukuk ve para konuları Semih sorusu olur; teknik seçimler sorulmaz.
- Park edilmiş görevlerle (V1-RMD-474, 519, 572, 588) çakışan bulgu "bilinen (V1-RMD-xxx)" işaretlenir, yeni görev açılmaz.

## 8. Kırmızı test yorumu

Denetim görevinde derleme veya test kırmızıysa bu bir bulgudur, görevin Done olmasını engellemez. `plan/TASK_STANDARD.md` içindeki "exit code 0" şartı uygulama görevleri içindir; denetim görevinde şart, çıktının gerçek exit code ile kayda alınmasıdır. Kapanış denetimleri (plan_audit, consistency_audit, task_scope) ise sıfır hata ve sıfır uyarı vermelidir.

## 9. Güvenli çalışma sınırları

- Ajanlar salt okunur çalışır; yalnız `tmp/qa/<görev>/<ajan>/` altına yazar.
- Test veritabanı: `alkaros-test-pg` (127.0.0.1:55432), `ALKAROS_TEST_PG_PORT=55432`, `ALKAROS_TEST_PG_PASSWORD=postgres`. dotnet `~/.dotnet/dotnet.exe`. Tüm Host.Tests koşusu 13-16 dakika sürer; arka planda koşulur, çıktı dosyaya yönlendirilir, bağlama yalnız özet alınır.
- Mutasyon yalnız qa-test-kalitesi tarafından, `git worktree add --detach "$TEMP/alk-mut-<modül>" HEAD` ile açılan kopyada, aynı anda tek koşu olarak yapılır; her turda yedekle, değiştir, test et, geri al, `cmp` ile doğrula; bitince worktree silinir. Mutasyon sırasında başka test koşulmaz.
- Yasak: canlı yığına (8090, 5080, 5433) veri yazmak, başka projenin konteynerlerine dokunmak, `compose up/build/restart`, `kvkk-retention --apply`, `audit-disposal`, gerçek dış servise istek, Telegram jetonunu herhangi bir yere yazmak.
- Veri yazan akışlar yalnız E2E düzeneğinde (kendi `alkaros_e2e_*` veritabanı ve Host) denenir, aynı anda tek koşu. Canlı yığına yalnız oturumsuz GET atılır.
- `MigrationComposition` testleri yerel psql ister; yalnız ana oturum çalıştırır, olmazsa "doğrulanamadı (altyapı)".
- Aramada `DROP` geçen desenler Bash grep yerine Grep aracıyla yapılır (hook engeli).

## 10. Semih raporu (en çok 10 satır, kod terimi yok)

1. Modül ve durum.
2. Bulgu sayıları (Kritik/Yüksek/Orta/Düşük; doğrulanan, çürütülen).
3-4. En önemli iki para veya hukuk etkisi.
5-7. Yalnız Semih'in kararını gerektiren sorular.
8. En değerli Fırsat.
9. Sıradaki adım.

## 11. Pilot sonrası ölçüm (595 sonunda doldurulur)

| Ölçü | Değer |
| --- | --- |
| Koşu başına ortalama token | yaklaşık 80 bin (53-118 bin); 595 toplam yaklaşık 805 bin |
| Koşu başına ortalama süre | 2,6-36 dk (ortalama 8 dk); en uzun qa-test-kalitesi |
| Ajan başına çürütme oranı | 595: tamamen çürütülen 0/26; abartılı önem 6 bulgu |
| Kılavuzda düzeltilen eksikler | bkz. §12 |

## 12. Pilot dersleri (V1-RMD-595)

- Ajanlar `evidence/` altına yazamaz (kapsam bekçisi); ana oturum ajan çıktısını `findings-raw.md` içinde birleştirir, ajan başına sayılar `agents/README.md` dosyasına yazılır.
- Bash komutlarında bazı veritabanı komut sözcükleri güvenlik freni tarafından engellenir; bu sözcükleri içeren metin Write aracıyla yazılır.
- Test PostgreSQL konteyneri (`alkaros-test-pg`) kapalı olabilir; ilk testten önce `docker ps -a` ile bakılır ve yalnız o konteyner `docker start` ile açılır. Aksi hâlde test hataları altyapı kaynaklıdır, bulgu değildir.
- Bash içinde `python3` ile heredoc kullanılmaz (asılı kalır); gerekirse `node -e` veya dosya yazma aracı kullanılır.
- Ajanlar paralel koşabilir (salt okunur); mutasyon koşan `qa-test-kalitesi` en uzun sürendir, önce o başlatılır.
- Ham bulguların yaklaşık yarısı aynı kök nedenin tekrarıdır (117 ham, 26 tekil); doğrulayıcıya yalnız birleştirilmiş liste verilir.
- Doğrulayıcı önemi sıklıkla düşürür (üretimde tetiklenemeyen gizli kusurlar): önem "bugün tetiklenebilir mi" sorusuyla yeniden değerlendirilir.
- Düzeltme görevleri kök neden gruplarına göre açılır (595 için V1-RMD-639...643).
