# V1-RMD-321 - `consistency_audit.py`'nin Türkçe-identifier tespiti yapısal olarak asla çalışmıyordu

- Task ID: V1-RMD-321
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K12 bulgusu: `IDENTIFIER_RE` (`tools/consistency-audit/consistency_audit.py:68-71`, satır numaraları düzeltme öncesi) yalnız `[A-Za-z0-9_]` karakter sınıfını kabul ediyordu — `var müşteriAdi` gibi bir identifier, Türkçe harften (`ü`) önce kesiliyordu (`identifier.group(0)` yalnız `"var m"` oluyordu). Kalan parçada Türkçe harf hiç aranmadığı için ihlal ASLA flag'lenmiyordu. Aracın ana amacının (kod identifier'larının İngilizce olmasını zorlamak) tam tersini üretiyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-321-consistency-audit-turkish-identifier-regex.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py

## In scope

1. `IDENTIFIER_RE`'nin karakter sınıfı, dosyanın kendi `TURKISH_CHARS` sabitini de kapsayacak şekilde genişletilir — böylece tam identifier yakalanır ve ardından gelen `TURKISH_RE.search(identifier.group(0))` kontrolü gerçekten çalışır.

## Out of scope

- Aracın diğer bilinen yapısal boşlukları (K11'in "kanıt metninin doğruluğunu değil biçimini kontrol ediyor" bulgusu, K13'ün CI-yalnız-PR bulgusu) — ayrı görevler.
- Orta seviye bulgu: aracın cross-schema yazma kontrolünün çok satırlı SQL'i, LIMIT kontrolünün yorum/string içindeki "limit" kelimesini kaçırması — ayrı, daha küçük bir orta seviye düzeltme.

## Dependencies

- None

## Acceptance evidence

Bu araç kendi kendini test eden bir Python betiği (bu repoda `tools/` altındaki hiçbir Python aracının kendi birim test paketi yok — hepsi doğrudan gerçek repoya karşı çalıştırılıp çıktısı doğrulanarak kanıtlanıyor, aynı bu görevde de yapıldı):

- Gerçek bir Türkçe identifier içeren geçici bir `.cs` dosyası (`var müşteriAdi = "test";`) repoya eklendi; `consistency_audit.py` çalıştırıldı — GERÇEKTEN flag'ledi: `Turkish character in identifier: var müşteriAdi = "test";`.
- Geçici dosya silindi, `consistency_audit.py` tekrar çalıştırıldı — `clean`.

Mutasyon kontrolü: `IDENTIFIER_RE` geçici olarak eski hâline (`[A-Za-z0-9_]`, Türkçe karakter yok) döndürüldü, AYNI geçici Türkçe-identifier dosyası tekrar eklendi — araç gerçekten `clean` döndü (ihlali kaçırdı, bulgunun tam olarak tarif ettiği davranış); dosya `diff` ile birebir orijinaline (düzeltilmiş hâline) geri getirildi, aynı senaryo tekrar doğru şekilde flag'lendi. Geçici test dosyası kalıcı olarak kaldırıldı — repoda iz bırakmadı.

## Handoff

- None
