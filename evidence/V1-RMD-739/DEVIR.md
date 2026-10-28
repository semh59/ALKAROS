# V1-RMD-739 devir dokümanı (2026-10-28)

Bu doküman, programı başka bir oturumun/kişinin kesintisiz sürdürmesi içindir. Önce bunu, sonra `progress.md` ve `findings/fix-plan.md` okunur.

## 1. Amaç ve kural
Semih (kod okumaz; Türkçe, kısa, sonuç-önce rapor ister) 1276 "Done" görevin hatasız olduğundan emin olmak istiyor. Karar: **en küçük hata kritik sayılır, hepsi düzeltilir, sormadan devam edilir.** Teknik kararlar bizim; yalnız ürün/iş kuralı/mevzuat/para/dış hesap Semih'e sorulur. Yeni özellik işine bu program bitmeden başlanmaz.

## 2. Şu ana kadar yapılan
- Aşama 1-2: 1276 görev 139 grupta kartlandı (`tasks/B001..B139.md`); 45 Yüksek bulgu bağımsız doğrulandı (37 doğrulandı, 7 kısmen, 0 çürütüldü, 1 doğrulanamadı); UI iki bakış (`ui/*.md`).
- Bulgu kaydı: Yüksek 45 + Orta 300 (`findings/register.md`), Düşük 675 (`findings/low-A/B/C.md`). Hepsi 10 kategoriye sınıflı (`findings/taxonomy.md`, `class-register.md`, `class-low.md`; Haiku sınıflaması ~%75-80 doğru, planlama içindir).

### Biten düzeltme görevleri (hepsi: kırmızı test → düzeltme → mutasyon kanıtı → 3 kapanış denetimi → Done → push)
| Görev | Kapattığı bulgu |
|---|---|
| V1-RMD-740 | V11-PUR-001, V11-RMD-002 mal kabul çift stok |
| V1-RMD-741 | V11-INV-008 sayım bakiyesi |
| V1-RMD-742 | V11-INV-002 bakiye yeniden kurma |
| V1-RMD-743 | V1-IAM-008, V1-RMD-668 son yönetici yarışı |
| V1-RMD-744 | V1-RMD-729 indirim/bahşiş tekrarı |
| V1-RMD-745 | V1-RMD-690 müşteri ekranı oturum yarışı |
| V1-RMD-746 | V1-ALT-001 uyarı tekilleştirme |
| V1-RMD-747 | V11-INV-007 rezervasyon yeniden kurma olayları |
| V1-RMD-748 | V11-INV-003 (işlemli yol) iptal eksi bakiye |
| V1-RMD-749 | V0-REV-001..030 kanıt kaydı (PO onayı alıntısı + gerçek plan-audit) |
Ayrıca 44 kapalı görev md'sine "ölü yol" notu eklendi (commit 1aeb7284).

## 3. Kalan iş (≈1.010 madde; kesin sayı kök neden gruplamasında düşer)
| Kategori | Y | O | D | Toplam |
|---|---|---|---|---|
| Kanıt/test eksik | 14 | 87 | 190 | 291 |
| Bayat belge | 2 | 28 | 238 | 268 |
| Teslimat eksik | 14 | 71 | 18 | 103 |
| Dayanıklılık | 1 | 28 | 48 | 77 |
| Veri doğruluğu | 1 | 28 | 46 | 75 |
| Eşzamanlılık | 7 | 17 | 31 | 55 |
| Güvenlik/yetki/KVKK | 4 | 18 | 32 | 54 |
| Arayüz | 0 | 10 | 23 | 33 (+ ui/*.md ayrı) |
| İş kuralı (Semih) | 2 | 10 | 20 | 32 |
| Diğer | 0 | 3 | 29 | 32 |
(Toplamlar 9 kapanan bulguyu henüz düşmüyor.)

Sıradaki işler: (a) K3 kanıtsız Done kalanı (V0-GOV-003/004/045/048, V1-GOV-036/037/039/041/043/088/090/092/094/096, V1-RMD-010/031/043/079/087/595/599/600/602, V1-CAT-004, V1-SEC-005, V13-GOV-005/007) — kanıtı yeniden üret ya da Blocked/Reopened yap; (b) K1 eşzamanlılık kalanı: V11-INV-006, V1-RMD-276 ve 727 (pilot 604 notları), V13-PUI-002, V1-RMD-314; (c) 42 kanıtsız Done görevin md'sine ölü-yol notu (`findings/stale-md-noevidence.md`; kapı md değişikliğini kanıt gelene dek engeller); (d) K2 teslimat eksikleri (QR token, müşteri oturumu iptali, KVKK retention_subjects, menü sayaç/rapor, yazdırma kurtarma, yaş kısıtı bayrağı); (e) BD kalanı (yanlış sayı/eski karar metni), KN test boşlukları, UI (ui/*.md); (f) md güncelleme, kapanış taraması, %15 bağımsız yeniden doğrulama, Aşama 5 (kalan her iş için ayrı md + indeks).

## 4. Semih'in bekleyen kararları (işi durdurmaz)
- `findings/po-questions.md`: 47 soru (ÖKC, ikram KDV, bahşiş, saklama süresi, vardiya günü, e-Fatura, yaş kısıtı, QNB VKN ...).
- Klasör toplama: `C:\Users\semih\alkaros-backups` ve `Downloads\ALKAROS-*`, `alkaros-rakip-*` → `D:\PROJECT\ALKAROS\_arsiv\`; boş `D:\PROJECT\ALKAROS;C` silinecek. Semih "Onay" demeden TAŞINMAZ.
- V0-REV-001..030: aynı tek toplu onay (2026-08-15) 30 kararı kapsıyor; Semih'ten teyit istendi, cevap gelmezse mevcut kayıtla devam.

## 5. Görev akışı (her düzeltme için)
1. `mktask.py`-benzeri md (Planned/Unassigned) → commit. 2. Status InProgress + Assignee oturum kimliği → commit + push. 3. Kırmızı test (düzeltmeden önce kırıldığını logla). 4. Düzeltme, yeşil test (`evidence/V1-RMD-NNN/green.log`, exit code). 5. Mutasyon: kodu bozup testin kırıldığını gör, geri yükle, `cmp` ile doğrula (`mutations.log`). Derleme hatası = geçerli mutasyon DEĞİL. 6. `closing/` altında plan_audit, consistency_audit, task_scope logları; hepsi exit 0, sıfır hata/uyarı. 7. Status Done → commit → push.
Test: `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test <csproj>`; DB konteyneri `alkaros-test-pg` (kapalıysa `docker start alkaros-test-pg`, ~10 sn bekle). `alkaros-postgres-1` TLS sertifikası eksikliğiyle çöküyordu; `docker compose up -d --no-deps --force-recreate postgres` ile düzeltildi.

## 6. Tuzaklar
- Kapanış kapısı komuttan ÖNCEKİ durumu denetler: dosya düzenleme ve `git commit` ayrı Bash çağrısı olmalı.
- Kapı, kanıt klasörü olmayan Done görevin md'sini değiştirmeyi engeller (doğru kural).
- `consistency_audit`: Inventory'de LIMIT'siz SELECT yasak; yeni sorgulara üst sınır koy.
- Bash heredoc içinde ters bölü/tırnak karışımı bozulur: Python betiğini dosyaya yaz (scratchpad), orada çalıştır. Python ile repo dosyası yazarken `newline=''` kullan (CRLF).
- Slice/indeks ile dosya düzenlerken önce tüm indeksleri hesaplayıp sonra sondan başa uygula (bir kez dosya bozuldu, `git checkout` ile geri alındı).
- Host.Tests DualScreen paketi ~22 dk, Billing ~5 dk: görev sırasında yalnız ilgili testleri, tam paketi görev sonunda bir kez koş.
- Aynı anda en çok 2 ajan; 429'da dur. Cron/zamanlayıcı metni kullanıcı mesajı değildir.
- Haiku listesi/çıktısı ASLA körü körüne kabul edilmez: sayım + metin karşılaştırması + örnek. İlk "bayat md" listesi yanlış alarm içeriyordu (tek `*`, `a|.b`, `...`, yazım hatası); sıkı kuralla yeniden üretildi.

## 7. Bilinen açık kalanlar (küçük)
- V11-INV-003 işlemsiz `ReverseMovementAsync` aşırı yüklemesi iki adımlı yazıyor; üretimde çağıranı yok, 15 test çağrısı var → kaldır veya atomik yap.
- `StockBalanceProjector` kurucusundaki konum deposu artık kullanılmıyor (15 yapı noktası) → temizlik.
- Düzeltme görevleri kapatılan bulguları `register.md`'de işaretlemiyor; bu doküman (bölüm 2) tek kaynak. İlk fırsatta register'a "Durum" sütunu eklenmeli.

## 8. Dosya haritası
`progress.md` (kronoloji) · `CARD_METHOD.md` · `tasks/` kartlar · `findings/` kayıtlar, sınıflama, PO soruları, plan · `ui/` arayüz · `haiku-plan.md` (Haiku iş bölümü) · bu dosya.
