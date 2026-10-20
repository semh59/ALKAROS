# V1-RMD-596 — BuildingBlocks modülü bağımsız denetim özeti

SHA: baseline/head.txt (c2359719; kaynak kod ajanların baktığı e03090d2 ile aynı, aradaki fark yalnız evidence). Dilim: 105 dosya (51 src .cs, 22 test .cs, 10 göç). Taban: 4 test projesi 99/99 yeşil, exit 0 (runs/).

## Sayılar
- Ham: 135 satır, 9 ajan (qa-backend 24, qa-veritabani 9, qa-sinir-mimari 18, qa-guvenlik-yetki 12, qa-test-kalitesi 26, qa-performans-dayaniklilik 14, qa-dokuman-bagimlilik 12, dar kapsamlı qa-operasyon 9 ve qa-mevzuat 11).
- Tekil: 26 kök neden (U01-U26; U26 16 alt madde).
- Doğrulayıcı (Opus): 19 DOĞRULANDI, 7 KISMEN, 0 ÇÜRÜTÜLDÜ, 4 alt madde DOĞRULANAMADI.
- Doğrulanan önem: Yüksek 2 (birim dönüştürücü, connector anahtarı), Orta 12, Düşük kalan. Ajanların Yüksek dediği 10 bulgudan 8'i indi (bugün tetiklenmiyor, kısmen çürük veya test/doküman konusu).
- Ajan başına tam çürütme: 0/9; abartılı önem: test (U15), performans (U08), güvenlik (U05), operasyon (U09).

## En önemli etkiler
1. Birim dönüştürücü, aynı türden iki birim (koli ve adet) için kaydedilen özel katsayıyı yok sayıyor; ekran kaydı kabul ediyor ama stok düşümü, fire ve mal kabul 1:1 hesaplanıyor (B001).
2. Docker kurulumunda QR tünel servisine şifre çözme anahtarı verilmemiş; QR halka açık yol bu kurulumda başlamaz (B002; Docker'da çalıştırılmadı).
3. Olay kuyruğu: sağlayıcı zaman aşımı, süreç çökmesi ve yeniden başlatma partiyi ~5 dk bekletiyor; hata nedeni hiçbir yere yazılmıyor; teslim edilmiş kayıtlar hiç silinmiyor.

## Temiz alanlar
Kira nesil çitlemesi, SKIP LOCKED eşzamanlılığı, geri çekilme formülü, ölü mektup yeniden kuyruğa alma (dryRun, aynı işlemde audit, yetki testleri), AES-GCM kullanımı (nonce rastgele, düz metin yok), sır çözümleme sırası, SQL tablo adı allow-list, UTC zaman, borç kalıpları 0.

## Doğrulanamayanlar
Mutant sayıları (loglar silindi), connector'ın Docker'daki gerçek davranışı, QR notunun sağlık verisi sayılması (KVKK sınıflandırması), U26'nın 4 alt maddesi, Secrets/SensitiveData test gövdelerinin bir kısmı.

## Fırsat tablosu
| ID | Fırsat |
|---|---|
| 596-F001 | Ölü mektup için yönetim ekranı ve tek tıkla "nedenini göster + yeniden dene" (şu an yalnız API; çalışanın işini azaltır). |
| 596-F002 | Birim kaydı ekranında koli/paket gibi katsayıyı kaydederken "1 koli = 12 adet" sonucunu anında önizleme. |
| 596-F003 | Açılışta eksik sır ve anahtar için tek ekranda "sistem hazır mı" kontrolü. |

## Bulgudan düzeltme görevine
B001,B004,B014 → V1-RMD-660 (birim dönüştürücü) · B003,B008,B009,B017,B018 → V1-RMD-661 (kira, iptal, zaman aşımı) · B005,B006,B007,B016,B021,B024 → V1-RMD-662 (görünürlük, temizlik) · B002,B012 → V1-RMD-663 (connector anahtarı, başlangıç doğrulaması) · B015,B019,B020,B022,B025,B026 → V1-RMD-664 (hassas veri sertleştirme) · B010 → V1-RMD-665 (mutfak olayı atomikliği) · B011,B013 → V1-RMD-666 (test ve mimari kapsam) · B023 → V1-RMD-667 (belge, lisans, manifest). Ayrıntı: findings.json.

## Semih metni (≤10 satır)
BuildingBlocks (olay kuyruğu, şifreleme, birim çevirici) denetlendi: 26 tekil bulgu, 19'u tam, 7'si kısmen doğrulandı, hiçbiri çürütülmedi. İki önemli: (1) "koli = 12 adet" gibi katsayı kaydediliyor ama stokta yok sayılıyor; (2) Docker kurulumunda QR dış bağlantı servisi anahtarsız kalıyor. Sorunuz yok; hepsi 8 düzeltme görevine bağlandı.

## Semih senaryosu
"Masa birleştirilir; hesap yeni masaya geçer ve yeniden başlatmadan sonra kaybolmaz." Bu senaryo masa birleştirme olayının kuyruk üzerinden gitmesine dayanır: birleştirme olayı aynı işlemde kuyruğa yazılıyor (PostgresTableMergeRepository.Merge.cs:247) ve yeniden başlatmada kira süresi sonunda yeniden teslim ediliyor (en çok ~5 dk gecikme, B009); kayıp yok. Hesabın taşındığı kısım Tables/Billing modüllerinin görevlerinde sınanacak; burada çalıştırılmadı (doğrulanamadı: uçtan uca).
