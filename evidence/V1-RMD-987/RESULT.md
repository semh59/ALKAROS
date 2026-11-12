# V1-RMD-987 Result

The bill split screen has a "Kişi ekle" button (only when the bill can be changed). People added there get a fresh id and take part in equal, by-item and by-amount splits; no seat or person has to be registered on the table first. People are numbered by position ("1. Kişi", "2. Kişi") so two people never share a label; an added person is selected at once in the equal split. The server already accepts any person id (no seat check applies to people), and a saved person comes back in the owner list.

- red.log: new tests fail before. green.log: billing 6 files pass. related.log: all billing and route tests, 353 of 353 pass. tsc clean.
- mutations.txt: 4 mutations killed, files restored (cmp).
