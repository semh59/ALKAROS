# V1-RMD-080 - Code identity English-only audit

- Task ID: V1-RMD-080
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`database/migrations/**` ve `src/**` içindeki tablo, kolon, index, constraint, sınıf, arayüz, metod, değişken, dosya isimleri ile kod yorumlarını sistematik olarak tarayıp İngilizce-yalnız kuralına uymayanları belirlemek ve düzeltmek. Kullanıcıya görünen string literal'ler taramadan muaftır.

## Owned surface

- `plan/v1/remediation/V1-RMD-080-code-identity-english-only-audit.md`
- `src/Modules/Identity/Authorization/IDenialEventSink.cs`
- `src/Modules/Identity/Authorization/PermissionCodes.cs`
- `evidence/V1-RMD-080/**`

## In scope

- `database/migrations/**` içindeki tüm şema kimliklerinin taranması; bulgu listesi ve sonucun `evidence/V1-RMD-080/` altına kaydı.
- `src/Modules/**`, `src/Host/**`, `src/Clients/**` içindeki tüm kod kimliklerinin ve yorum satırlarının taranması; bulgu listesi ve sonucun kayıt altına alınması.
- Bulunan gerçek ihlallerin düzeltilmesi: `IDenialEventSink.cs` ve `PermissionCodes.cs` içindeki İngilizce özet yorumlarına gömülü Türkçe alıntı ifadelerin İngilizceye çevrilmesi.
- Para alt birimi `kuruş` teriminin İngilizce karşılığı olmayan resmi bir özel ad olarak kod yorumlarında korunacağının kayıt altına alınması.

## Out of scope

- Kullanıcıya görünen Türkçe string literal'leri değiştirmek.
- Davranış değiştiren herhangi bir kod düzenlemesi.

## Dependencies

- V1-RMD-079

## Deliverables

- Şema ve kod kimliği tarama raporu; iki yorum ihlali düzeltilmiş kaynak dosyalar.

## Acceptance evidence

- `dotnet test` Identity Authorization testleri sıfır hata verir (yorum değişikliği davranışı etkilemez).
- Semih; `evidence/V1-RMD-080/` altındaki tarama raporunda migration ve kod kimliklerinin temiz, iki yorum ihlalinin düzeltilmiş olduğunu doğrular.

## Handoff

- V1-RMD-081
