# V1-RMD-599 — Host modülü bağımsız test ve kalite denetimi

SHA d1dc86b6 (denetimin sabitlendiği commit). Bu görev kod değiştirmez. 13 bağımsız ajan + karşı denetçi (11 parti), ana oturum Yüksek bulguyu kodda yeniden açtı.

## Sayılar
| Ölçü | Değer |
| --- | --- |
| Ham bulgu (13 ajan) | 279 |
| Tekil bulgu (kök nedene göre) | 144 (131 hata, 13 doküman sapması) + 10 fırsat |
| Doğrulayıcı sonrası önem | Kritik 0, Yüksek 1, Orta 44, Düşük 99 |
| Doğrulayıcı kararı | DOĞRULANDI 102, KISMEN 41, ÇÜRÜTÜLDÜ 1 (B142), DOĞRULANAMADI 0 |
| Önem düşürülen | 33 bulgu (6 Yüksek→Orta; 27 Orta→Düşük) |
| Düzeltme görevine bağlanan | 139 (V1-RMD-688..699); 4 bilinen/dış görevde (V13-HUG-001, V1-RMD-335, V1-RMD-596, Semih sorusu S8) |
| Semih sorusu | 9 (aşağıda) |

Ham sayı ajan başına: güvenlik 25, API 21, backend 38, mimari 29, veritabanı 18, performans 16, operasyon 28, entegrasyon 27, doküman 18, mevzuat 11, frontend 18, UX 18, test kalitesi 12 (+19 mutant). Her ajan 53/53 (frontend 15/15, mevzuat 24/24, UX 44/44 dar kapsam) kalem raporladı; başlangıç sayımı 53 (48 src/Host + 5 göç), sapmalar ajan raporlarında açıklı.

Tekil bulgular `findings-raw.md`, makine okunur hâli ve karar/önem/düzeltme görevi `findings.json`, doğrulayıcı tabloları `verifier/` altındadır.

## Yüksek bulgu (ana oturum kodda yeniden açtı)
**B001 — Tünel yolu rota izolasyonu.** `DualScreenApplication.cs:464-466` bağlayıcı alt ağını (`--nfc-trusted-network 172.28.90.0/28`) yalnız HTTPS muafiyetinde kullanıyor; NFC/QR rota izolasyonu (`:560-587`) bu ağı tanımıyor (`NfcTrustedNetworks` kodda yalnız `:465`'te). Sonuç: tünel yolunda QR siparişi çalışmaz (QR/NFC 404) ve yönetim/kasa uçlarına izolasyonsuz ulaşılabilir görünür. Güvenlik, operasyon ve entegrasyon ajanları bağımsız buldu, doğrulayıcı teyit etti. **Doğrulanamayan:** cloudflared'in `X-Forwarded-*` başlıklarını gerçekten nasıl ilettiği (internetten erişimin hangi dalda gerçekleşeceği); bu yüzden Kritik değil. Kimlik doğrulama atlanmıyor. Düzeltme: V1-RMD-688.

Doğrulayıcı B002–B007'yi (ajanlar Yüksek saymıştı) Orta'ya indirdi; gerekçeleri `verifier/verdict-y1.md` ve `verdict-y2.md`.

## Semih senaryosu (müşteri ekranı)
Eşleştirilir, ürün eklenince anında görünür, eşleştirme iptalinde ekran veri göstermez — **çalışıyor** (kod okuması, 4 ajan):
- Eşleştirme: kasada "Ekranı eşleştir", 8 karakterli kod (2 dk geçerli), yaklaşık 5 dokunuş.
- Ürün ekleme: her sipariş değişikliğinden sonra sunucu ekranı bilgilendirir; ekran ayrıca 5 sn'de bir yoklar.
- İptal: iptalden en çok 5 sn sonra ekran 401 alır ve eşleştirme koduna döner; eski ürünler gösterilmez.
Eksikler: iptal ve eşleştirme akışının sunucu testi yok (B003); ekran ağ hatasında kendiliğinden toparlanmıyor (B004); 12 saatlik mutlak oturum vardiya ortasında kodu istetiyor (B015); iptal onaysız (B070). **Doğrulanamadı:** gerçek tarayıcıda/cihazda elle deneme.

## Test ve derleme kayıtları (`runs/`, gerçek exit code)
ApiConventions 5/5, ModuleBoundaries 10/10, Host.Errors 180/180, Host MigrationComposition 196/196 (exit 0); tsc exit 0; oxlint 3 jsx-a11y uyarısı. **Host.Observability koşusu bellek yüzünden yarım kaldı (exit=1, log kesik, başarısız test satırı yok): doğrulanamadı (altyapı).** Mutasyon (qa-test-kalitesi, `mutation/`): 19 geçerli mutant, 12 öldü, 7 yaşadı (hata kataloğu ve mimari kenar testi boşlukları).

## Ajan başına çürütme
Tümüyle çürütülen: 1 (B142 bildirim matrisi — belge karar kaydı). Ajan iddialarından düzeltilenler: test-kalitesi "HelpRequests/WebPush testi yok" (testler var), mimari B-09 etkisi, backend/entegrasyon "hub kimlik sızıntısı" abartısı, doküman D-06 kısmen, frontend F8 "eski hata koduyla taklit" (sunucu kodu üretiyor), B029 "test 0" (3 dosya var), B033 "tek kova" (gönderilen kurulumda geçerli değil). Oran düşük (~%1 tam çürütme, ~%29 KISMEN); hiçbir ajan yeniden koşturulmadı. Doğrulayıcının kaydı: bir parti `/tmp` altına geçici dosya yazıp sildi (izin dışı, zararsız).

## Temiz alanlar
Hata kataloğunda ham istisna mesajı sızıntısı yok; parola/PIN saklama sağlam (PBKDF2-SHA256, 600k); oturum belirteçleri hash'li; Host'ta dize birleştirmeli SQL yok; webhook'larda sabit-zamanlı karşılaştırma; göç koşucusu atomik (`--single-transaction`) ve sağlama toplamı korumalı; 27 izin kodunun hepsi kullanımda; yardım çağrısı eşzamanlılığı advisory lock altında; WaiterPresenceTracker bellek sınırlı; ApiErrorCatalog yaklaşık 180 eşlemeyi birebir sınıyor; Ayarlar zinciri (682–687) filtresi temiz; lisans (Npgsql, MIT/MPL-2.0/ISC) politikaya uygun.

## Doğrulanamayanlar
cloudflared başlık davranışı (B001 internet yolu); gerçek push servisi ve cihaz davranışı; SignalR yeniden bağlanma/geri basınç çalışma zamanı; EXPLAIN (B055); ağ gerektiren bağımlılık taraması; Host.Observability koşusu; satır kapsama aracı; tarayıcıda elle deneme. Alıntı betiği (`checks/citations.log`): 549 tekil dosya:satır alıntısından 534'ü SHA'da bulundu; 15 kayıt (çoğu `Program.cs`, kısa ad birden çok dosyaya eşleştiği için) betikçe bulunamadı, doğrulayıcı bunları ayrıca kodda açtı.

## Fırsat tablosu (Semih'in ürün kararı)
| Kod | Fırsat | Etki |
| --- | --- | --- |
| F001 | Eşleştirmeyi tek dokunuşa indirmek | Kasiyer 5 adım yerine 1 |
| F002 | Kasada ekranın "bağlı" rozeti | Ekran koptu mu kasiyer görür |
| F003 | İptalde onay ve "geri al" | Yanlışlıkla iptal azalır |
| F004 | Yardım çağrısında "görüldü" geri bildirimi | Garson çağrının ulaştığını bilir |
| F005 | Bekleyen hesap için ses/rozet | Kasada kaçan hesap azalır |
| F006 | Bildirim izni için yönlendirme | Garson push'u kendi açar |
| F007 | Kasaya boşta kilit | Yetkisiz kullanım azalır |
| F008 | Hata kodu → Türkçe sözlük (tek yer) | Tutarlı mesaj, bakım kolay |
| F009 | İptal sonrası marka ekranı | Müşteri ekranı boşken profesyonel görünür |
| F010 | Hub/push/anlık görüntü/outbox yük senaryosu | Ölçüsüz riskler ölçülür |

## Semih soruları (yalnız ürün/hukuk/para)
1. S1: Devir notu ve yardım çağrısı kayıtları ne kadar saklansın? (önerim: 90 gün)
2. S2: Anonimleştirilen personelin abonelik/not/çağrı kayıtları silinsin mi, anonim mi kalsın? (önerim: abonelik silinsin, çağrı kaydı anonim kalsın)
3. S3: Push adresi kişisel veri sayılsın mı? (önerim: evet)
4. S4: VAPID özel anahtarı veritabanında düz kalabilir mi? (önerim: şifrelensin; teknik, ben yaparım)
5. S5: Müşteri vergi kimliği için yalnız değişiklik mi, her okuma da mı kaydedilsin? (önerim: ikisi de)
6. S6: Pasifleşen personele bildirim gitmesi kabul edilebilir mi? (önerim: hayır, kesilsin; teknik)
7. S7: Silme talebi sonrası yedekten geri gelen veri nasıl ele alınsın?
8. S8: "Kart çekilmedi" tek yetkili kişiyle kapanabilsin mi?
9. S9: Masa taslağından (garson) açılan siparişler müşteri ekranında görünsün mü?

## Düzeltme görevleri (kök nedene bağlı, V1-RMD-599-B..)
| Görev | Kapsam | Bulgular |
| --- | --- | --- |
| V1-RMD-688 | Tünel yolu rota izolasyonu ve güvenlik başlıkları (Yüksek) | B001, B030, B075, B136 |
| V1-RMD-689 | Yardım çağrısı: kalıcı, geri bildirimli, kasada görünür | B002, B067, B073, B082, N001 |
| V1-RMD-690 | Müşteri ekranı: toparlanma, oturum, eşleştirme/iptal deneyimi ve sunucu testleri | B003, B004, B015, B017–B020, B027–B028, B066, B069–B070, B074, B087, B114, B121, B126, B129 |
| V1-RMD-691 | Bildirim (push): SSRF, sıralı gönderim, hata, sınır, pasif kullanıcı, etiket, testler | B005, B008–B013, B029, B059–B063, B071–B072, B098 |
| V1-RMD-692 | KVKK ve saklama (S1–S3, S5) | B006, B007, B014, B043, B106, B109, B113, B115, B138 |
| V1-RMD-693 | Hata kataloğu ve zarf | B021, B022, B024, B065, B083–B085, B089, B092, B096, B132, B134 |
| V1-RMD-694 | Kasa, fatura ve sipariş akışı | B023, B051–B053, B056–B057, B086, B102, B130 |
| V1-RMD-695 | Modül sınırı, kayıt ve mimari test | B025–B026, B048–B050, B090–B091, B093–B095, B097, B099–B101, B104–B105 |
| V1-RMD-696 | Hız sınırı, kimlik, hub oturumu, DB rolü | B016, B031–B036, B064, B077–B080, B088, B103, B116–B117 |
| V1-RMD-697 | Yedek, kurtarma, dağıtım | B037–B042, B044–B046, B107–B108, B118–B120, B125 |
| V1-RMD-698 | Göç satır sonu, indeks, şema | B047, B054–B055, B110–B112 |
| V1-RMD-699 | İstemci erişilebilirlik ve doküman sapmaları | B068, B122, B123, B127–B128, B131, B133, B135, B137, B139–B141, B143 |

Bilinen/dış: B058→V13-HUG-001 (kart tahsilatı, dış bağımlılık), B076→V1-RMD-335 (CSP/HSTS kararı), B124→V1-RMD-596, B081→Semih sorusu S8.

Sıra: Yüksek B001 (688) 600'den önce; 689–699 Orta/Düşük, faz sonunda toplu (kalıcı talimat gereği hepsi düzeltilecek).

## Doküman sapmaları (kod esastır)
topology.md eski/yanlış (B131), api-contract-standard.md ve hata kodu adı (B132), idempotency anahtarı gövdede (B133, bilinen), iki hata zarfı (B134), operasyon belgeleri (B135), yanlış güvenlik yorumları (B136), yorum–kod çelişkileri (B137), KVKK runbook PIN özeti (B138 — gerçek açık, 692'de), CLI usage (B139), order.json 139≠197 (B140, bilinen), eski belge ifadeleri (B141), katalog dışı izin kodları (B143). settings-ownership.md:66 "history immutable ayrı görev" ifadesi 197 ile bayatladı (D-14).

## Kapanış
`closing/` altında plan_audit, consistency_audit ve task_scope çıktıları.
