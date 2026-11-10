# V1-RMD-911 - İndirim ve geri alma yolu tahsil edilen tutarı iadeleri düşerek hesaplar (tek ortak yardımcı)

- Task ID: V1-RMD-911
- Status: InProgress
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-906 açığı (koordinatör kararı: ertelenmez). İkram yolu, hesapta tahsil edilen tutarı "ödeme dağıtımları toplamı eksi iade (reversal) toplamı" olarak hesaplar; indirim yolu (uygula ve ön kontrol) ve ayar geri alma yolu yalnız dağıtım toplamına bakar. İade edilmiş bir ödemenin tutarı hâlâ tahsil sayıldığından, nakit iadesi sonrası geçerli bir indirim yanlışlıkla "tahsil edilenin altında" diye reddedilir ve bir bahşiş geri alınamaz. Karar: hesaplama tek ortak yardımcıya (BillMoneyGuard) taşınır; ikram, indirim (uygula + ön kontrol) ve geri alma aynı yardımcıyı kullanır.

## Owned surface

- `evidence/V1-RMD-911/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Billing/BillMoneyGuard.cs
  - src/Host/Experience/Billing/BillingSplitStore.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - tests/Host/Experience/Billing/CollectedNettingHttpTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Testler: iade edilmiş ödemeli hesapta indirim uygulanır (eskiden yanlış reddedilirdi); iadeden sonra kalan tahsilatın altına inen indirim hâlâ reddedilir; iade sonrası bahşiş geri alınabilir
2. Ortak yardımcı ve üç çağrı yerinin değişimi
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Bekleyen veya onaylanmış kalem iadesi olan hesapta indirim engeli (V1-RMD-909 davranışı değişmez)

## Dependencies

- V1-RMD-910

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None
