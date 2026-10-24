# Kırmızı testler (taban koşu, SHA feb38368)
1. PaymentTenderHttpTests.cs:709 ACheckThatWasAlreadySentToTheTillCompletesButItsTableIsNeverTouched: beklenen ("Occupied", null), gerçek ("Available", null). PaymentTender.log 1/53, exit=1.
2. BillingSplitHttpTests.cs:104 EndpointsEnforceSessionPermissionAndAtomicConcurrency: allocation id sırası beklenenle farklı (Expected [def7..., 264f...], Actual [264f..., def7...]). Billing.log 1/71, exit=1. Sıralama kararsız olabilir (ORDER BY eksikliği şüphesi, doğrulanmadı).
