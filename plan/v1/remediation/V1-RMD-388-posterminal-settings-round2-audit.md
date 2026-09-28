# V1-RMD-388 - PosTerminal Ayarlar ekranları Tur 2 denetimi: oturum sonlandırıldığında sessiz kalıyordu

- Task ID: V1-RMD-388
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetiminin Tur 2'si (P1/P2/P4/T7/T8 derin geçişi), Modül 13: PosTerminal'in
7 Ayarlar ekranı (RelaySettings, QnbCredentialSettings, TokenTerminalSettings,
SecurityAdministration, BusinessIdentitySettings, ReservationStation,
CustomerDisplayScreensaverSettings). Bu 7 ekranın kendisi zaten olgun — hepsi aynı
login/durum/form iskeletini paylaşıyor, T1/T3/T6 Tur 1'de zaten işlenmişti.

Ama T7 (rol-arası haberleşme) açısından, `SecurityAdministration.tsx`'in kendisi bizzat bir
başka role gerçek bir etkide bulunan tek ekran: "Tüm Oturumları Sonlandır" düğmesi, o kullanıcının
başka bir terminaldeki (ör. Cashier veya WaiterPwa) AKTİF oturumunu anında geçersiz kılıyor.
`Cashier.tsx`'in kendi `registerSessionExpiredHandler`'ı (V1-RMD-291, "herhangi bir çağrıdan gelen
401'i düşürecek tek yer") bunu yakalayıp sessizce `session === "anonymous"`'a düşürüyordu — etkilenen
kasiyer, aktif bir satışın ortasında birden boş bir giriş ekranına düşüyor, NEDEN olduğuna dair hiçbir
ipucu görmüyordu (uygulama çökmüş gibi algılanabilir; oturum süresi normal şekilde dolduğunda da aynı
sessizlik geçerliydi). Bu, doğrudan bu modülün kendi eylemi (başka bir yöneticinin oturum sonlandırma
kararı) ile bir başka rolün ekranı arasındaki gerçek bir haberleşme boşluğuydu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx
- `src/Clients/PosTerminal/src/routes/Cashier.session-ended-message.test.tsx`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-388-posterminal-settings-round2-audit.md`

## In scope

1. **[T7, Yüksek — rol-arası haberleşme] Oturumu bir başka yöneticinin sonlandırması (veya oturumun
   normal şekilde süresinin dolması) sessizce oluyordu.** `Cashier.tsx`'e yalnızca
   `registerSessionExpiredHandler`'ın kendi geri çağrısında set edilen bir `sessionEndedMessage`
   durumu eklendi — ilk yüklemede `restoreSession`'ın kendi 401 yakalaması (normal "henüz giriş
   yapılmamış" durumu) bu mesajı HİÇ tetiklemiyor, yalnızca daha önce `session === "ready"` iken
   gerçekten koparılan bir oturum tetikliyor. Giriş ekranına "Oturumunuz sonlandırıldı. Lütfen
   tekrar giriş yapın." uyarısı eklendi; bir sonraki giriş denemesi başlatıldığında (`login()`'in
   başında) bu mesaj temizleniyor, böylece yeni denemenin kendi hata/başarı geri bildirimiyle
   karışmıyor.

## Out of scope

- WaiterPwa (vanilla JS istemci) aynı sınıftan bir boşluk taşıyor olabilir, ama bu modülün (Modül 13)
  kapsamı PosTerminal'in Ayarlar ekranlarıyla sınırlı; WaiterPwa Tur 2'de zaten kendi modülünde
  (Modül 4, V1-RMD-379) derinlemesine ele alındı ve bu bulgu o geçişte gözlemlenmedi — ayrı bir
  görev gerektirir, Semih'in kararına bırakılıyor.
- Diğer 6 ayarlar ekranı (Relay/Qnb/Token/Security'nin arama kısmı/BusinessIdentity/Reservation/
  Screensaver) kendi başlarına T7/P1/P2/P4/T8 açısından incelendi, gerçek bir ek bulgu çıkmadı —
  hepsi tek-oturumluk, düşük sıklıkta kullanılan admin formları; performans/mobil kaygısı
  uygulanamaz, rakip karşılaştırması anlamlı değil (ALKAROS'a özgü entegrasyon ekranları).

## Dependencies

- None

## Acceptance evidence

- `src/Clients/PosTerminal`: `npx tsc --noEmit` sıfır hata; `npx vitest run` tam paketi (40 dosya,
  297 test, bu görevin yeni testi dahil): 297/297 geçti, regresyon yok.
- Mutation-check: yalnızca `Cashier.tsx` `git stash` ile geri alındı — yeni test GERÇEKTEN kırmızı
  oldu (`AssertionError: expected '...' to contain 'Oturumunuz sonlandırıldı...'`). `git stash pop`
  ile geri yüklendi, tam paket tekrar 297/297 yeşile döndü.

## Handoff

- None
