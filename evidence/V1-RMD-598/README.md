# V1-RMD-598 — Settings modülü bağımsız kalite denetimi

SHA: eb93ec697d6f67c46012f62610550fd3e32c670c. Kod değişmedi. Sonuç: Settings sağlam çalışıyor ama Semih senaryosu bugün baştan sona sağlanmıyor.

## Semih senaryosu (kabul 5): SAĞLANMIYOR
- "Garson başına masa sınırı 3 yap": ekranda yok (yalnız ad, renk, logo düzenlenir). Ayar ilk okunana dek kayıtsız, API ile bile 404 olabilir.
- "4. masa açılırken Türkçe uyarı": yok. Sınır yalnız "önerilen garson" listesini süzer; masa açma ve devir yolları sınıra bakmaz. Sayaç da yanlış: masa değil sipariş sayıyor, servis edilmiş masayı saymıyor.
- "Geçmişte kim değiştirdi": kişi adı yerine kullanıcı kimliğinin ilk 8 hanesi görünür.

## Sayılar
- Ham: ~110 bulgu (9 ajan), tekil: 24 kök neden (+1 doğrulayıcının eklediği: N1), doğrulanan 21 kök + TK 10, KISMEN 5+3, çürütülen 2 (TK-B9, UX-B12), doğrulanamayan 4 (çerez SameSite/CSRF, logo CSP/nosniff, business.name kaçışlaması, EXPLAIN).
- Önem: Kritik 0, Yüksek 0, Orta 12, Düşük çoğunluk (ayrıntı `findings.json`). Doğrulayıcı R1/R2/R3'ü Orta saydı; Semih senaryosu nedeniyle bunlar ilk düzeltilecekler.
- Ajan başına çürütme: test kalitesi 1, UX 1, diğerleri 0 (oran düşük, ajan yeniden koşturulmadı).
- Taban: .NET 95/95, vitest 20/20, tsc 0 (`runs/`). Mutasyon (qa-test-kalitesi): 19 mutantın büyük kısmı yaşadı (ajanın iki sayımı tutarsız; bulgular tek tek doğrulandı). En zayıf: logo imza/boyut, ChangedBy, moduleOwner süzgeci, bozuk değerde varsayılan.

## Temiz alanlar
Yetki filtresi (settings.manage), parametreli SQL, FOR UPDATE + row_version, güncelleme ve geçmiş tek işlemde, logo tür beyaz listesi (SVG yok) ve 5 MB sınırı, Türkçe hata zarfı, altı özel ayarın varsayılanları doğru (0, 9, false, 5 dk, false, "", lacivert).

## Fırsatlar
| Fırsat | Etki |
| --- | --- |
| Tek ayar ekranı: satıra tıkla, değer + geçmiş yan yana, "Ali 4 → 3 yaptı" özeti | Müdür teknik destek istemeden ayar değiştirir; öğrenme eşiği düşer |
| Ayar kataloğu (anahtar, tür, aralık, varsayılan, etiket) tek kaynak | Doğrulama, ekran ve sahiplik tek yerden |
| Garson seçicide "sınır dolu" rozeti | Kasiyer neden önerilmediğini görür |

## Düzeltme görevleri (kök nedenlere bağlı)
| Görev | Kapsam |
| --- | --- |
| V1-RMD-682 | N1 sayaç + R1 masa açma/devir sınırı ve Türkçe uyarı |
| V1-RMD-683 | R4, R5, R6, R13, R15 sunucu doğrulaması (aralık, uzunluk, palet, pasif ayar, kayıt yarışı, sır listesi, izin sabiti) |
| V1-RMD-684 | R2, R3, R12a-g ayar düzenleme ekranı, kişi adları, istemci düzeltmeleri |
| V1-RMD-685 | R7, R8, R16 logo (okuma yükü, denetim izi, imza/boyut testleri, 136 sertleştirme) |
| V1-RMD-686 | R9, R10, R11, R14, R17 geçmiş sağlamlaştırma, N+1, DI, test boşlukları, doküman sapmaları |

Sıra: 683 ekrandan (684) önce; 682'de önce sayaç, sonra denetim.

## Doküman sapmaları (kod esastır)
settings-ownership.md: "registry" yok (ModuleOwner serbest metin), RequiresRestart etkisiz, "history silinmez" şemayla zorlanmıyor.

## Doğrulanamayanlar
Çerez SameSite/CSRF, logo yanıtında CSP/nosniff, business.name kaçışlaması (QR sayfası dilim dışı), indekslerin EXPLAIN'i, tarayıcıda elle deneme.
