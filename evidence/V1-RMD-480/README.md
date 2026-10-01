# V1-RMD-480 kanıtı

- `task-scope-tests.log`: `python -m pytest tests/Architecture/TaskScope` -> 161 passed, exit code 0 (kabul testleri üç göreve genişletildi).
- Mutasyon: araçtaki sabit kümeden `V15-KVK-002` kaydı silinince 4 kabul testi kırmızı; dosya geri alındı, diff yalnız beklenen iki kayıt.
- Kapsam: `GATES.md` tablosu araçtaki kayıtlarla birebir eşleşmezse kapı kapalı kalır; yalnız `V15-KVK-001`, `V15-PER-001`, `V15-KVK-002` kabul edilir.
