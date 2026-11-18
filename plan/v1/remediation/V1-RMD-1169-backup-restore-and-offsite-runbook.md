# V1-RMD-1169 - Sade Türkçe "Yedekten geri dönüş" prosedürü ve yedek zamanlaması/hedef ayarı belgeleri

- Task ID: V1-RMD-1169
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-1167 yedeği otomatik yaptığına göre belgeler gerçeği anlatmalıdır. (1) `docs/operations/yedekten-geri-donus.md`: işletme sahibi için adım adım Türkçe prosedür (hangi komut, ne kadar sürer; D2'de ölçülen 9 sn alma / 55 sn geri yükleme referansı; canlı veriyi ezmeden ayrı veritabanında geri yükleme, kimlik oturumlarını iptal ve ana anahtar uyarısı dahil). (2) Makine dışı kopya hedefinin ayarlanması sade adımlarla (hedef bilgisi Semih'te: sürücü harfi, UNC'nin çalışmadığı ölçümü, ilk tur sonrası dosyanın gerçekten geldiğini kontrol). (3) `rpo-rto-targets.md` §3 "budama elle" ifadesi, `backup-restore-runbook.md` saklama ve cron bölümü ve `production-go-live-checklist.md` §3 maddeleri yeni gerçeğe göre güncellenir.

## Owned surface

- `evidence/V1-RMD-1169/**`
- `docs/operations/yedekten-geri-donus.md` — yeni sade prosedür.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - docs/recovery/rpo-rto-targets.md
  - docs/recovery/backup-restore-runbook.md
  - docs/operations/production-go-live-checklist.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Yazılan her komut ayrı test veritabanında (tek kullanımlık kapsayıcı) gerçekten koşulur; süreler evidence'a yazılır; deneme sonunda test veritabanı silinir
2. İki bağımsız qa incelemesi (komut doğruluğu ve belge/kod tutarlılığı)
3. Belge ile kod arasında eski ifadeler kalmaz (consistency audit)

## Out of scope

- Betik veya compose değişikliği; canlı veritabanında geri yükleme
- Windows açılışında Docker Desktop'ın kendiliğinden başlaması (checklist 14, Semih/dış taraf)

## Dependencies

- V1-RMD-1168

## Acceptance evidence

1. Komut dökümü ve süreler (ayrı test veritabanı), silme kanıtı
2. Plan audit ve consistency audit 0 hata 0 uyarı
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None
