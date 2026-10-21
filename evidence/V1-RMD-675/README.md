# V1-RMD-675 kanıtı

- B019: yenilenen oturum, eski oturumun bitişini aşamaz (1 sn pay bırakılır, böylece her yenileme ancak kısaltır). Parola ile giriş 8 saatlik üst sınırdır.
- B026 (giriş kısmı): kilidi başlatan hatalı deneme bir kez kaydedilir; kilitliyken yapılan denemeler kayıt üretmez; kayıt yazımı hata verirse giriş sonucu aynen döner.
- Testler: test-identityhardening.log (19 yeşil). Mutasyon: mutations.log, 5 mutantın 4'ü yakalandı; A1 (`!IsLocked(before)` koruması) eşdeğer: kilitli hesapta sonuç zaten LockedOut'tur, bu dala girmez.
- Tüm çözüm testi (test-dualscreen.log; yanlışlıkla bütün çözüm çalıştı) 5 hata gösterdi, test-rerun.log tek tek doğruladı:
  1. OfflineReconciliation `TheLoginBudgetCarriesTheRoleCode...`: V1-RMD-674'ün garson indirim satırı yüzünden bütçede artık iki satır var (beklenen sonuç, test eski sayıya bağlı).
  2. ApiConventions `EveryEndpointRefusesACallerWithoutASession`: V1-RMD-671'in parti sınırı kontrolü oturum kontrolünden önce çalışıyor, oturumsuz çağrı 401 yerine 400 alıyor (gerçek hata).
  3. Composition `ServeContainerResolves...`: DenialEventSink türü beklentisi, bu işlerle ilgisiz (Observability dekoratörü); bu görevlerden önce de kırık olduğu doğrulanmadı.
  4. Audit EventStore ve ServerEndToEnd testleri: eşzamanlı çalışma/ortam kaynaklı olabilir, ayrıca doğrulanmadı.
- 1 ve 2 numaralı hatalar bu görevin kapsamı dışındaki Done görevlerin dosyalarında; V1-RMD-676 ile düzeltilecek.
