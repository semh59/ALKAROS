# V1-RMD-1137 - Nakit ödemede çekmece beklemesi en çok 1 sn ve çekmece sonucu ödeme yanıtında

- Task ID: V1-RMD-1137
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 1): yazıcı yanıt vermezken nakit ödeme ekranı 3,7 sn'de döndü (çekmece kapalıyken 0,24 sn); ödeme başarılı ve çift kayıt yok, ama bekleme hedefin (en çok ~1 sn ek) üstünde. Ödemenin başarısı çekmeceye bağlı kalır DEĞİL, çift kayıt olmaz.

1. Gönderici zaman sınırı 2 sn'den 1 sn'ye iner (`TcpCashDrawerKicker.DefaultTimeLimit`); yerel ağdaki yazıcıya bağlantı milisaniyedir, 1 sn'yi aşan yazıcı zaten yanıtsızdır. Sınır hem nakit ödeme hem "Satış yok" için geçerlidir.
2. `cash-tender` ucunda çekmece gönderimi, hesabı kapatma ve müşteri ekranı bildirimi (`TryCloseBillAsync`) ile AYNI ANDA başlar; uç ikisini de bekleyip yanıtlar. Böylece çekmece beklemesi hesap kapatmanın süresiyle örtüşür. Tekrar (`WasReplayed`) çekmeceyi açmaz (mevcut kural).
3. Ödeme yanıtı çekmece sonucunu taşır: `drawerOpened` = `true` (darbe teslim edildi), `false` (denendi, ulaşılamadı), `null` (çekmece kapalı ya da tekrar; hiç denenmedi). Mevcut alanlar (`approvedAmount`, `changeAmount` vb.) aynen kalır. `KickForSaleAsync` bu sonucu döndürür; hiçbir durumda atmaz ve ödemenin cevabını bozmaz.
4. Önce gerçek gecikmenin kaynağı ölçülür (gönderim, denetim kaydı, hesap kapatma); 1 sn sınırı ve eşzamanlılık ölçümle gerekçelendirilir. Ölçüm kanıtı `evidence/V1-RMD-1137/` altına yazılır; ek gecikme başka bir yerdeyse bulgu RESULT'ta raporlanır ve kapsam dışı parça ayrı görev olur.

## Owned surface

- `evidence/V1-RMD-1137/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.CashSession.cs
  - src/Host/DualScreen/CashDrawerService.cs
  - src/Modules/Kitchen/PrintQueue/CashDrawerKick.cs
  - tests/Host/Experience/CashSession/CashDrawerHttpTests.cs
  - tests/Modules/Kitchen/PrintQueue/CashDrawerKickTests.cs
  - docs/domain/cash-session-design.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. CashSession HTTP testleri, Kitchen PrintQueue testleri ve ApiConventions mimari testi

## Out of scope

- Ödeme ekranındaki uyarı metni (V1-RMD-1139); "Satış yok" sayacı ve uyarısı (V1-RMD-1138); yazıcı durumu sorgusu, yeniden deneme, arka plan kuyruğu; gerçek cihaz denemesi

## Dependencies

- V1-RMD-1136

## Acceptance evidence

1. Test çıktıları, mutant listesi, gecikme ölçümü ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. ApiConventions mimari testi exit code 0

## Handoff

- V1-RMD-1139
