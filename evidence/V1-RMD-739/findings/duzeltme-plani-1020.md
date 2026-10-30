# ALKAROS — 1.020 bulgu: kategori ve düzeltme planı

Kaynak: evidence/V1-RMD-739/findings (27–28 Ekim, commit 4aca38c7). Sınıflama: classification/ANA_KAYITLAR.json.

## Özet

- Toplam kayıt: 1020
- Düzeltilecek veya doğrulanacak: 989
- Bilgi notu (kusur değil, kayıt amaçlı): 28
- Kusur değil (kanıtla elendi): 3
- Daha önce tam kapandığı kayıtla eşleşen: 16; kısmi kapanan, kalan kusuru olan: 29

## Kural (her bulgu için)

1. Bugünkü kodda yeniden doğrula. Rapor 74 commit önce yazıldı; 975 satır yalnız kod okumasıyla iddia edildi.
2. Yanlış çıkarsa kanıtla kapat; düzeltme yapılmaz.
3. Doğruysa: kırmızı test → düzeltme → yeşil test → mutasyon kanıtı → iki bağımsız inceleme → Done.
4. Belge hatası: tarihçe korunur, güncellik notu + belge guard testi.
5. Aynı modülde aynı kategoriden bulgular tek görevde toplanır; farklı davranış ise ayrı görev olur.

## Dalga 1 — para, veri, güvenlik (179)

- **EZ — Eşzamanlılık ve atomiklik: 50 bulgu, 17 alan.** En yoğun: V1-RMD 15, Inventory 6, Identity 4, Billing 3, CustomerAccounts 3, Menu 3
- **VD — Veri doğruluğu ve hesap: 83 bulgu, 31 alan.** En yoğun: V1-RMD 20, Observability 8, PosTerminal 5, Kitchen 4, Cashier 3, QrOrdering 3
- **GY — Güvenlik, yetki, KVKK: 46 bulgu, 19 alan.** En yoğun: V1-RMD 15, Security 4, Orders 3, QrOrdering 2, Privacy 2, PosTerminal 2

## Dalga 2 — eksik teslimat ve dayanıklılık (149)

- **TE — Eksik teslimat / bağlanmamış kod: 83 bulgu, 24 alan.** En yoğun: V0-REV 20, V1-RMD 13, Observability 12, V1-GOV 5, PosTerminal 4, Kitchen 3
- **DY — Dayanıklılık ve performans: 66 bulgu, 22 alan.** En yoğun: V1-RMD 22, Observability 9, PosTerminal 5, Kitchen 3, Operations 3, Security 2

## Dalga 3 — arayüz, kod kalitesi, mimari (69)

- **UI — Arayüz ve erişilebilirlik: 30 bulgu, 8 alan.** En yoğun: V1-RMD 14, PosTerminal 5, Cashier 4, V12-CWB 3, Kitchen 1, V1-WTR 1
- **BK — Kod kalitesi (ölü kod, teknik borç): 27 bulgu, 10 alan.** En yoğun: V1-RMD 13, Billing 2, PosTerminal 2, Inventory 2, Observability 2, Reporting 2
- **MK — Mimari sınır ve sahiplik: 12 bulgu, 11 alan.** En yoğun: V1-RMD 2, V1-FND 1, Audit 1, Privacy 1, Catalog 1, V0-DAT 1

## Dalga 4 — test/kanıt ve belgeler (555)

- **KN — Kanıt ve test eksik: 283 bulgu, 46 alan.** En yoğun: V1-RMD 88, V1-GOV 52, WaiterPwa 14, Observability 13, PosTerminal 12, V0-GOV 11
- **BD — Bayat belge: 272 bulgu, 49 alan.** En yoğun: V1-RMD 79, V1-GOV 21, Cashier 20, V0-REV 20, V1-FND 13, PosTerminal 11

## Karar bekleyen — Semih (37)

- **PO — Semih kararı gereken iş kuralı/mevzuat: 37 bulgu, 16 alan.** En yoğun: V1-RMD 15, V0-CMP 5, V13-GOV 2, Cashier 2, V0-GOV 2, V0-ARC 1

## Hazırlık işi (Dalga 1 öncesi)

- 16 tam kapanış ve 740–749 görevlerine bağlı 21 kaydı register üzerinde işaretle; kanıtla teyit et.
- Paket 02 (F0944/F0945) yanlış kabulünü geri çek.
- 300 paketlik eski gruplama bırakılır; görevler bu dosyadaki kategori × alan sırasıyla açılır.

## Bilinen sınırlar

- Kategoriler önceki sınıflandırmadan; satır bazında bağımsız doğrulanmadı. Yeniden doğrulamada kategori değişebilir.
- PO kayıtları 47 soruya bağlı; Semih cevabı olmadan kod yazılmaz.
- Kesin görev sayısı yeniden doğrulamadan sonra belli olur; kategori × alan kaba üst sınırı yaklaşık 250 görevdir, aynı kök nedenler birleşince düşer.
