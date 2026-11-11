# V1-RMD-955 Result

Status: Done.

- The drawer screen button now reads "Kasayı Kapat: Sayıma Başla" with a line under it: the count is the first step of closing and cannot be undone once started.
- Tests: cash-session suites 98/98; 2 mutants killed.
- Open product note: there is no way back from a started count to an open drawer (no cancel path in the lifecycle). Adding one would be a rule change (Counting -> Open).
