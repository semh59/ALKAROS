# V1-RMD-263 - Garson kiosk kilidi sayfa yenilemeyle aşılamaz

- Task ID: V1-RMD-263
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

E2E ana planı bulgusu (güvenlik): PIN kilidi yalnız bellekte (`state.locked`)
tutuluyordu. Kilit ekranındayken sayfayı yenileyen biri, oturum hâlâ geçerli
olduğundan doğrudan sipariş listesine düşüyordu; kilit tek F5 ile aşılıyordu.
Kilit durumu artık `localStorage` içinde kalıcıdır; açılışta oturum geçerli ve
PIN kurulu ise ekran yeniden kilitlenir. Doğru PIN, 409 (hesapta PIN yok) ve
giriş ekranına devir kilit bayrağını temizler.

## Owned surface

- `plan/v1/remediation/V1-RMD-263-kiosk-lock-survives-page-reload.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/kiosk-lock.js
  (V1-WTR sahipliğinde kalır — yalnız kalıcı kilit bayrağı ve yeniden uygulama)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/auth.js
  (yalnız girişe devirde bayrağın temizlenmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/waiter-app.js
  (yalnız `init` sonunda yeniden kilitleme çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/07-kiosk-lock.spec.js
  (V1-RMD-260 ile eklenen paketin yeni senaryosu)

## In scope

1. Kilit bayrağının kalıcılığı ve açılışta yeniden kilitleme.
2. Gerçek tarayıcı senaryosu: kilitliyken yenile → hâlâ kilitli ve arka plan
   etkileşimsiz; doğru PIN → yenile → kilitsiz.

## Out of scope

- Bayrak `localStorage` içinde olduğundan, cihaz üzerinde geliştirici araçlarına
  erişimi olan biri bayrağı silebilir; gerçek sınır sunucu oturumudur. Bu görev
  yalnızca sıradan yenileme yolunu kapatır.

## Dependencies

- V1-RMD-260

## Acceptance evidence

- WaiterPwa E2E `07-kiosk-lock`: 12/12 geçti (UTF8 Postgres 18).
- Mutasyon kontrolü: `src` değişikliği geri alınınca yeni senaryo beklenen yerde
  kırıldı (kilit katmanı görünmedi); geri uygulanınca geçti.
- `plan-audit validate` ve `consistency_audit.py` çalıştırıldı.

## Handoff

- None
