# V1-RMD-286 - Kasa yardım çağrısı bağlantısı kopunca geri döner

- Task ID: V1-RMD-286
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-25

## Goal

`Cashier.tsx` içindeki `/hubs/help-requests` bağlantısı da 5 yeniden denemeden sonra kalıcı kapanıyor, `start()` hatası yutuluyor ve yeniden bağlanınca kaçırılan çağrılar için bir telafi yok. Kasa, masa yardım çağrısını hiç görmeden çalışmaya devam eder. Bu görev bağlantıyı sürekli yeniden deneyen, geri geldiğinde açık yardım çağrılarını yeniden yükleyen ve durumu Türkçe gösteren hâle getirir.

## Owned surface

- `plan/v1/remediation/V1-RMD-286-cashier-help-hub-resilience.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
  (yalnız yardım çağrısı bağlantısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.help-alerts.test.tsx

## In scope

1. İlk başlatma hatasında da sonsuz yeniden deneme.
2. Yeniden bağlanınca kaçırılmış olabilecek çağrılar için uyarı (sunucuda açık çağrıları listeleyen uç nokta yok, yeniden okuma yapılamaz).
3. Türkçe bağlantı durumu göstergesi; yalnızca bir kez kararlı bağlanmış (yetkili) oturumda görünür, hub'ın reddettiği yalnız kasiyer oturumunda görünmez.

## Out of scope

- Yardım çağrısının sunucuda hedeflenmesi (`V1-RMD-289`).

## Dependencies

- V1-WTR-011

## Acceptance evidence

- PosTerminal vitest: 27 dosya, 204/204 (3 yeni). Yeni testler: ilk `start()` hatasında 1 sn ve 2 sn sonra yeniden başlatma; kararlı bağlantı kopunca 'yeniden bağlanılıyor', geri gelince kapatılabilir 'görülmemiş olabilir' uyarısı; hub'ın hemen kapattığı oturumda uyarı yok ve bekleme süresi sıfırlanmaz (1 sn, sonra 2 sn).
- Mutasyon kontrolü: `Cashier.tsx` eski hâline döndürülünce 3 yeni test kırıldı.
- `pnpm typecheck` temiz. `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.
- Kapsam notu: görev 'açık yardım çağrılarını yeniden yükle' diyordu; bunu sağlayacak uç nokta yok (yalnız `POST` var), sunucu değişikliği kapsam dışı. Yerine kaçırılmış olabileceği uyarısı eklendi. Gerçek tarayıcıda ağ kesme denenmedi.

## Handoff

- None
