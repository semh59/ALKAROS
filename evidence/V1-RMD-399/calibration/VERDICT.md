# Kalibrasyon kararı (mühür açılmadan önce yazıldı)

- Zaman (UTC): 2026-09-28T10:14:54Z
- Mühür: `SEAL.sha256` (seed.patch ve SEALED.txt SHA-256, 2026-09-28T09:58:09Z).
- Kör koşu: `probes/run-1-blind.log` (16 geçti / 9 başarısız).
- Tohumlu koşu: `calibration/probe-run-seeded.log`.
- Fark: yalnız `A5DeactivatedUserSessionIsRejected` geçti → başarısız oldu; diğer 24 probe ve tarama çıktıları aynı.
- Karar: **YAKALANDI.** Tahmin: tohum, pasifleştirilmiş kullanıcının (`identity.users.active = false`) kasiyer
  oturumunu kabul ettiren bir değişiklik (oturum doğrulama sorgusundan `active` koşulunun düşürülmesi ya da eşdeğeri).
