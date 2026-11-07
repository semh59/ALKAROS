# V1-RMD-874 incelemeler

1. qa-veritabani: bloklayan hata yok. Orta: Pending kısmi indeksiyle örtüşme gerekçesi yazılmamıştı (RESULT'a eklendi). Düşük: docs/data/migration-dependency-graph.md:26 "şu an 219" diyor (220'de de bayattı; kapsam dışı belge, açık), ManifestTests açıklama zincirinde 221 satırı yok (kozmetik).
2. qa-test-kalitesi: orta 2 (alt dize ile indeks kontrolü kırılgan, mutant seti dar): test katalog sorgusuna çevrildi (kolon sırası, benzersizlik, kısmilik), UNIQUE ve kolon sırası mutantları eklendi ve öldü. Düşük: ikinci Migrate ile IF NOT EXISTS idempotansı ayrı sınanmıyor.
3. Doğrulama turu: hedefli koşu 19/19, beş mutant öldü.
