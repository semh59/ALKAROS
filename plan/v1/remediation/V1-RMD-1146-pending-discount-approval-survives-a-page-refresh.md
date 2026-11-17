# V1-RMD-1146 - Bekleyen indirim ve ikram onay talebi sayfa yenilenince ekrana geri gelir

- Task ID: V1-RMD-1146
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 15 (bulgu 7): yönetici onayı bekleyen indirim ya da ikram talebi sunucuda durur, ama sayfa yenilenince Hesap Ödeme ekranından kaybolur (talebin anahtarı ve numarası yalnız sayfa belleğindeydi); kasiyer "Yönetici burada onaylasın" alanına da ulaşamaz ve aynı talebi yeniden gönderince ikinci bir talep açılır.

1. Talep yönetici onayına gittiğinde (sunucu `Pending` yanıtı) talebin kimliği (`grantId`), yineleme anahtarı ve gövdesi tarayıcının `sessionStorage` alanına, hesap numarasıyla birlikte yazılır; indirim ve ikram ayrı tutulur. Şifre ya da yönetici adı yazılmaz.
2. Sayfa aynı hesap için yeniden açılınca kayıt okunur: "onay bekleniyor" durumu, form değerleri, gönderildi bildirimi ve "Yönetici burada onaylasın" bölümü geri gelir; mevcut yoklama aynı anahtarla talebi yeniden gönderir, böylece sunucu tek sonuç verir (onaylandıysa uygulanır, reddedildiyse Türkçe cümle, hâlâ bekliyorsa sessiz).
3. Kayıt, talep karara bağlanınca (uygulandı, reddedildi, bulunamadı), form düzenlenince ya da başka bir hesap açılınca silinir. `sessionStorage` kullanılamıyorsa ekran eskisi gibi çalışır.
4. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1146/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Clients/StaticApps/split-payment-approval-restore.test.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-discount-pending.test.js
  - tests/Clients/StaticApps/split-payment-comp.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci testleri (tests/Clients/StaticApps)

## Out of scope

- Sunucu tarafı bekleyen talep listesi; yönetim ekranı; başka sayfalar (kasa kapanışı)

## Dependencies

- V1-RMD-1145

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi
3. tests/Clients/StaticApps testleri exit code 0

## Handoff

- None
