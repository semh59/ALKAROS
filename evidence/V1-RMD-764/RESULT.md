# V1-RMD-764 sonuç

Bulgu V15-KVK-002: yasal bekletme yazımdan önce denetlenmiyordu; kişi listelendikten sonra konan bekletme verinin silinmesini engellemiyor, iş yalnız sonradan "Blocked" oluyordu. Mevcut test işin Blocked olduğunu doğruluyor ama verinin silindiğini doğrulamıyordu.

Düzeltme: `ApplyStepAsync` her adımın yazımından önce, aynı transaction ve hesap kilidi içinde `privacy.legal_holds` içinde etkin bekletme arar; varsa `RetentionSubjectHeldException` ile durur, `ProcessAsync` işi "legal hold" Blocked olarak kaydeder.

## Kanıt
- Kırmızı (`red/`): iş satırı açılınca bekletme koyan tetikleyici ile yeni test düştü (veri silinmişti, `Actual: False`); önceki 12 test geçti.
- Yeşil (`green/`): `ALKAROS.Privacy.Anonymization.Tests` (gerçek PostgreSQL) 13/13, exit 0.
- Mutasyon (`mutation-no-hold-check/`): denetim `if (false && ...)` ile etkisizleştirilince yeni test düştü, 12 geçti; kaynak `cmp` ile geri yüklendi.

## Sınırlar
- Kontrol ile yazım aynı transaction'da ama bekletme koyma hesap kilidiyle serileştirilmez: kontrol ile yazım arasındaki dar pencerede konan bekletme hâlâ kaçabilir.
- Yakın hareket ve bekleyen talep yeniden denetimi (KVK-001 devir notunun diğer yarısı) bu görevde değil.
