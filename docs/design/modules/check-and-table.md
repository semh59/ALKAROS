# Hesap ve Masa — ayrı iki şey

> **Durum:** Onaylandı (Semih, 2026-09-10).
> **Kapsam:** Sipariş/masa/hesap yaşam döngüsü. Bu doküman
> `docs/design/foundations.md`'nin modül kararları zincirinin bir parçası;
> oradaki §0 ilkesi ("backend akıllı, frontend aptal") burada da geçerli.
> **Neden şimdi:** Semih'in sorduğu somut senaryo (2026-09-10): *"masa yemeği
> yedi kalktı, kasada sıra var ödeme için, o masaya da yeni müşteri geldi ne
> yapacağız"*. Sistemin bugünkü cevabı: eski hesabı kaybediyor.

## 1. Bugünkü davranış (kod okunarak doğrulandı)

Masa 5'in müşterileri kalkıp kasaya gidiyor; sistemde bunu işaretleyen hiçbir
şey yok. Yeni müşteri oturuyor:

1. `GET /orders/table/{id}` masanın **en yeni canlı siparişini** döndürüyor
   (`OrderManagementStore.cs:298-314`, `ORDER BY created_at DESC LIMIT 1`).
   Garson masaya dokununca adisyonda **eski müşterilerin yemekleri** çıkıyor.
2. Yeni sipariş gönderilince, eski sipariş `Draft` olmadığı için birleştirme
   dalı ıskalanıyor (`:554-566`, `WHERE o.status = 'Draft'`) ve **ikinci bir
   sipariş** açılıyor.
3. `current_order_id` koşulsuz olarak yeni siparişe kaydırılıyor
   (`:178-188`) — durum kontrolü yok. **Eski hesap öksüz kalıyor:** hiçbir
   masa onu göstermiyor, garson ekranında ona giden yol yok, masa toplamı
   yalnız yeni müşteriyi gösteriyor.
4. Masayı boşaltmak (`SetAvailable`) yalnız `current_status`'ü değiştiriyor,
   `current_order_id`'yi temizlemiyor
   (`PostgresTableRepository.cs:120-144`) — bayat işaretçi kalıyor.

**Kök sebep:** hiçbir sipariş kapanmıyor. `TransitionTo(OrderState.…)`
çağrılarının tamamı tarandı: yalnız `Submitted`, `Accepted`/`Rejected`
(sadece QR/NFC) ve `Cancelled` gerçekten çağrılıyor. `Preparing`, `Ready`,
`Served`, `Completed` durumlarına geçen **tek bir çağrı yok**. Garson siparişi
süresiz `Submitted` kalıyor ve `GetActiveOrderByTableIdAsync` `Submitted`'ı
canlı saydığı için masanın son siparişi sonsuza kadar "masanın açık hesabı"
oluyor. Sistemde "bu grup bitti" diyen bir durum hiç yok.

## 2. Karar: hesap masaya değil, müşteri grubuna aittir

Masa mobilyadır; hesap bir müşteri grubuna aittir. Grup masadan kalkıp kasada
beklerken hesabı hâlâ açıktır ve **masa o anda yeni gruba verilebilir**.

`table_mgmt.tables.current_order_id` bundan sonra "bu masaya şu an bağlı olan
hesap" anlamına gelir — masanın tarihi değil. Bir hesap masaya bağlıyken
masanındır; kasaya gönderilince masadan **kopar** ve kasanın kuyruğuna geçer.

### 2.1 Onaylanan akış: "Hesabı kasaya gönder"

| Adım | Ne olur |
| --- | --- |
| Garson *Hesabı kasaya gönder* der | Sipariş yeni kaleme kapanır; hesap (bill) yoksa oluşturulur; `current_order_id` temizlenir; masa `Cleaning`'e geçer |
| Masa | Anında yeni müşteriye açılabilir — garson *Temizlendi* deyince `Available` |
| Kasa | Hesap "ödeme bekleyen hesaplar" listesinde, masa numarasıyla değil **hesap numarasıyla** durur |
| Yeni müşteri | Masaya dokununca boş adisyon açılır; eski hesabın kalemleri görünmez |

Reddedilen iki seçenek (kayıt için): *masada birden fazla açık hesap* — garson
unutsa da veri kaybolmaz ama her ekranda "hangi hesap" sorusu çıkar, yoğun
serviste yavaşlatır; *ödeme bitmeden masa açılamaz* — veri kaybı imkânsız ama
kasada sıra varken masa boş bekler, ki sorunun çıkış noktası tam olarak buydu.

### 2.2 Yeni tur artık aynı hesaba eklenir

Birleştirme araması `status = 'Draft'` yerine **masaya bağlı hesabı** arar.
Garson istemcisi hiçbir zaman `Draft` bırakmadığı için bugün her tur yeni bir
sipariş açıyordu; masa ₺400 meze + ₺900 ana yemek söylediğinde adisyon ₺900
gösteriyor ve ₺400 hiç faturalanmıyordu. Bağlı hesap kavramı bunu da kapatır —
aynı kökten gelen iki kusur, tek düzeltme.

### 2.3 Sessiz kayıp yerine açık soru

`current_order_id`, masaya bağlı ve kasaya gönderilmemiş bir hesap varken
**asla** başka bir siparişe kaydırılmaz. Böyle bir durumda backend isteği
reddeder ve garsona sorulur: *"M-05'te kapanmamış bir hesap var. Kasaya
gönderilsin mi?"* Bugünkü koşulsuz `UPDATE` sessizce veri kaybettiriyor;
kural, kaybı bir soruya çevirir.

## 3. Kapsam dışı — bilerek

- **Ödeme.** Semih'in kararı (2026-09-10): şimdilik dokunulmuyor. V1'de bir
  hesabı `Paid` yapan uç nokta yok ve bu kasıtlı bir V1.2 sınırı. Sonucu
  açıkça kabul edildi: **"ödeme bekleyen hesaplar" listesi kendiliğinden
  boşalmaz**, gün sonunda elle kapatılması gerekir. Ödeme yöntemi, para üstü
  ve kasa devri V1.2'nin işidir.
- **Sipariş yaşam döngüsünün tamamı.** `Preparing`/`Ready`/`Served`/
  `Completed` geçişlerinin hiç çağrılmıyor olması ayrı ve daha büyük bir
  eksik; bu doküman onu çözmez, yalnız hesabın masadan kopmasını çözer.
  Ayrı bir görevde ele alınmalı.
- **Masa birleştirme/bölme** (`/merges`) ve bölünmüş hesap (split) akışları
  bu kararla çelişmez ama burada ele alınmaz.

## 4. Bu kararın dokunduğu yerler

- `orders`: masaya bağlı hesabın aranması, hesabın masadan koparılması.
- `table_mgmt`: `current_order_id`'nin temizlenmesi ve korunması.
- `billing`: kasa için "ödeme bekleyen hesaplar" listesi (bugün böyle bir uç
  nokta yok — `GET` yalnız split-design grubunda var).
- Garson PWA: *Hesabı kasaya gönder* eylemi ve kapanmamış hesap uyarısı.
- Kasa/PosTerminal: bekleyen hesaplar kuyruğu — kendi görevinde.
