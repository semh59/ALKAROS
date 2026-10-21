# V1-RMD-676 kanıtı

- Parti sınırı kontrolü oturum doğrulamasından sonraya alındı; oturumsuz çağrı yine 401 alır. Bütçe testi garson indirim satırını bekler.
- Testler: test-runs.log (OfflineReconciliation 13, ApiConventions 5 yeşil). Mutasyon: mutations.log (kontrolü öne alan mutant yakalandı).
- Açık: Composition (DenialEventSink türü), Audit EventStore ve ServerEndToEnd kırmızıları bu görevlerle ilgisiz; ayrıca incelenmedi.
