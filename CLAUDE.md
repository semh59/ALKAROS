# ALKAROS Agent & Claude Execution Contract

Bu dosya, repository içinde kod yazan bütün AI agent oturumları (Claude Code, Antigravity vb.) için zorunludur.

Tüm agent'lar aşağıdaki bağlayıcı sözleşmelere ve kılavuzlara kesinlikle uymak zorundadır:

1. **Agent Yürütme Sözleşmesi:** AGENTS.md
   - Tek görev sınırı: Her kodlama oturumu başlamadan önce tam olarak bir Task ID seçilir.
   - Yazılabilir yüzey: Yalnızca aktif görevin Owned surface yolları ve evidence/<Task-ID>/** yazılabilir.
   - Zorunlu preflight: Root, Task ID, git snapshot ve allowlist çıkarılır.
   - Kapsam dışına çıkma yasağı: İlgisiz refactor, gizli kapsam genişletme, unapproved dependency yasaktır.
   - Kod doğruluğu: Kod, semboller ve testler İngilizce; kullanıcı açıklamaları Türkçe.
   - Kapanış kapısı: Allowlist diff kontrolü, build/test exit code 0, gerçek kanıt üretimi.

2. **Arayüz Standartları:** docs/UI_STYLE_GUIDE.md
   - Kullanıcıya görünen her metin Türkçedir (etiketler, butonlar, rozetler, hata mesajları, aria-label).
   - Ham error.message, HTTP kodları veya İngilizce enum'lar asla ekrana doğrudan basılamaz; Türkçe çeviri sözlüğünden geçer.
   - Periyodik İngilizce sızıntı taraması (docs/UI_STYLE_GUIDE.md §4) uygulanır.

3. **Plan ve Sahiplik Standartları:**
   - plan/TASK_STANDARD.md
   - plan/OWNERSHIP.md
   - plan/GATES.md

4. **Kapanış ve Doğrulama Şartı:**
   - Branch main'e alınmadan önce veya gate kapatılmadan önce mutlaka:
     \python tools/plan-audit/plan_audit_tool.py validate\
     ve
     \python tools/consistency-audit/consistency_audit.py\
     çalıştırılır; sıfır hata ve sıfır uyarı zorunludur.
