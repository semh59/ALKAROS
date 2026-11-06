# V1-RMD-836 - İkram yetki bütçesi, kalem değiştirilebilirlik kontrolünden önce harcanıyor

- Task ID: V1-RMD-836
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesi G11 grubu, kayıt F0049 (V1-BIL-005): ikram uç noktası (`POST .../items/{itemId}/comp`) `bills.comp` yetkisi olmayan kullanıcı için önce yetki talebini (`grants.RequestAsync`) açıyor, kalemin değiştirilebilirlik kontrolünü (`RequireItemChangeableAsync`: sipariş tamamlanmış/iptal/reddedilmiş veya hesap ödenmiş) ancak sonra, tekrar oynatma kapsülünün içinde yapıyor. Garson kapanmış siparişin kalemi için ikram dener ve kişisel bütçe verilirse ikram uygulanmaz ama günlük bütçe harcanmış kalır (başka bir kalemde kullanamaz).

Düzeltme: kontrol, yetki talebinden önce, yalnız `bills.comp` yetkisi olmayan dalda yapılır. Kapsülün içindeki kontrol kalır (aynı anahtarla tekrar gönderilen uygulanmış ikram ilk sonucu döndürmeye devam eder).

## Owned surface

- `evidence/V1-RMD-836/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-BIL-005) — kontrol sırası.
  - tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs (V1-WTR-012) — yeni testler.
  - tests/Host/Experience/Orders/Comp/OrderManagementCompTestDatabase.cs (V1-RMD-408) — yetki kaydı sayım yardımcısı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Önce kırmızı test (gerçek Postgres): garson, tamamlanmış siparişin kendi kalemi için ikram dener; cevap 409 `ORDER_CLOSED`, o kalem için yetki kaydı oluşmaz, günlük bütçe harcanmaz. Aynı kural ödenmiş hesap için (`BILL_NOT_MODIFIABLE`).
2. Düzeltme, yeşil, mutasyon turu (`cp` + `cmp` ile geri alınır).
3. İngilizce kod ve test adları.

## Out of scope

- `void-sent` ucu (bütçe harcamaz; yalnız bekleyen yetki kaydı bırakabilir, ayrı bulgu olarak raporlanır).

## Dependencies

- V1-BIL-005

## Acceptance evidence

1. Comp HTTP test projesi tam yeşil; kırmızı çıktı ve yeşil çıktı `evidence/V1-RMD-836/` altında.
2. Mutasyon: kontrol yetki talebinin arkasına taşınınca yeni testler kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
