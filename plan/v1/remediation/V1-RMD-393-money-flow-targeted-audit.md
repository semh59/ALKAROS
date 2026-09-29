# V1-RMD-393 - Para akışı hedefli derin denetimi (sipariş → hesap → tahsilat → kasa → gün sonu)

- Task ID: V1-RMD-393
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: investigation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

`Done` işaretli para akışını (sipariş → hesap → bölme → indirim/bahşiş → tahsilat → hesap kapanışı → kasa
oturumu → gün sonu raporu → ödeme mutabakatı) kod okumasıyla değil, gerçek PostgreSQL üzerinde çalışan
yeniden üretim (probe) testleriyle denetlemek. Her iddia çalıştırılmış bir kanıta bağlanır; bulunan her hata
ayrı bir düzeltme görevi olarak raporlanır, bu görevde üretim kodu değiştirilmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-393-money-flow-targeted-audit.md`
- `evidence/V1-RMD-393/**`

## In scope

- Para akışı invariant'larının yazılı listesi (ör. ödenen toplam = dağıtılan toplam; hesap yalnız ödenecek
  tutar indirim/bahşiş sonrası karşılandığında kapanır; kasa beklenen tutarı = nakit tahsilat − nakit iade ±
  kasa hareketleri; gün sonu cirosu = iş gününe düşen onaylı ödemeler).
- Akış adımı × durum geçişi × hata yolu (yeniden deneme, eşzamanlılık, kısmi hata) kapsam matrisi; her hücre
  "sağlam (kanıtıyla)", "bulgu" veya "denetlenemedi (nedeniyle)" olarak kapatılır.
- Gerçek PostgreSQL 18 üzerinde çalışan probe testleri; kaynak ve çalıştırma çıktısı kanıt klasöründe.
- Kritik korumalarda mutasyon denetimi: koruma geçici olarak bozulur, mevcut testlerin bunu yakalayıp
  yakalamadığı ölçülür, üretim kodu geri alınır.
- Kalibrasyon: denetim başlamadan bilinen bir hata bağımsız olarak tohumlanır; denetimin onu bulup bulmadığı
  raporlanır.
- Her bulgunun ayrı, önceki bağlamı bilmeyen bir doğrulayıcı tarafından çürütülmeye çalışılması.

## Out of scope

- Üretim kodu, test projeleri, migration veya başka görevin plan dosyasında değişiklik.
- Stok/reçete akışı ve "yazılıyor ama okunmuyor" taraması: ayrı denetim görevleri.
- Dış entegrasyonlar (Token/Beko terminali, mali belge, yemek kartı, QNB): gerçek erişim yok.
- İade akışı: V13-ALC-004 olarak zaten açık (V1-RMD-328).

## Dependencies

- None

## Acceptance evidence

- `evidence/V1-RMD-393/REPORT.md`: invariant listesi, doldurulmuş kapsam matrisi, bulgular (dosya:satır,
  yeniden üretim adımı, probe testi adı, doğrulayıcı sonucu), mutasyon sonuçları, kalibrasyon sonucu ve
  denetlenemeyen alanlar.
- `evidence/V1-RMD-393/probes/`: probe testlerinin kaynağı ve gerçek çalıştırma çıktısı (exit code dahil).
- Referans koşusu: tam `dotnet test ALKAROS.slnx` çıktısının özeti (geçen/kalan/atlanan sayıları).
- Semih'in elle deneyebileceği senaryo: her bulgu için rapordaki yeniden üretim adımları.

## Handoff

- None
