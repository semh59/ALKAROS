# V1-RMD-273 - Çalışan uygulamadan çağrılmayan kayıtlı servislerin kapatılması

- Task ID: V1-RMD-273
- Status: NotApplicable
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

`V1-RMD-272` erişilebilirlik kapısı, DI'da kayıtlı ama `src/Host/**` üzerinden hiçbir başvuru yolu ile
ulaşılamayan 38 türü listeledi (`tools/consistency-audit/unreachable_services_allowlist.json`). Her aile için,
kod okunarak şunlardan BİRİ kanıtlanmalı ve liste girişleri silinmeli:

1. **Gerçekten ölü:** çalışan uygulamaya bir çağıran (uç nokta, tüketici, zamanlayıcı) eklenir ve çağıran
   üzerinden uçtan uca test yazılır; ya da
2. **Kapsam dışı / gelecek göreve ait:** ilgili plan görevi ve nedeni yazılıp tür `NotApplicable` kararıyla
   listeden çıkarılır (kod silinir ya da gelecek görev referansıyla bırakılır); ya da
3. **Analiz kör noktası:** kapı bu türü yanlış işaretlemiş (ör. adla değil dize ile çözülüyor); kural düzeltilir.

Aileler: porsiyon rezervasyonu (yaşam döngüsü, çakışma hakemi, iptal etkisi, mutfak kalem durumu sağlayıcısı),
rezervasyon bakiye izdüşümü, reçete sürümleme ve birim dönüşümü, fire kaydı, stok hareket defteri, iade niyeti,
hesap ödeme kapanış izdüşümü, mutfak yönlendirme servisi, V15 güvenlik servisleri (oturum rotasyonu — ne zaman
döndürüleceği ürün kararı —, sır çözümleyici, yeniden şifreleme).

**Kapanış:** sınıflandırma yapıldı; ölü ve gerekli olanlar `V1-RMD-274/275/276/277` ile bağlandı, kalanlar `V1-RMD-278` kararıyla gerekçelendirildi. Bu görev ayrı bir uygulama gerektirmediği için `NotApplicable`.

## Owned surface

- `plan/v1/remediation/V1-RMD-273-unreachable-registered-services-backlog.md`

## In scope

1. Her ailenin kod okunarak sınıflandırılması (ölü / kapsam dışı / kör nokta) ve kararın kaydı.
2. Ölü olanlara çağıran eklenmesi; her biri için gerçek tarayıcı ya da gerçek HTTP kanıtı.

## Out of scope

- Yeni özellik tasarlamak: yalnız var olan servislerin çalışan uygulamaya bağlanması ya da bilinçli dışlanması.

## Dependencies

- V1-RMD-272

## Acceptance evidence

- `unreachable_services_allowlist.json` boş ya da yalnız bilinçli, gelecek-görev referanslı girişler içerir.
- Her bağlanan aile için "kullanıcı yolculuğu" testi (giriş → görünür gezinme/uç nokta) kırılmadan geçer.
- `python tools/consistency-audit/consistency_audit.py` ve
  `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata verir.

## Handoff

- None
