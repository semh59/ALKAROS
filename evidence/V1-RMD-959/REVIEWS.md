# V1-RMD-959 Reviews

## ui-turkce-metin-uzmani
- Medium: the account-created text pointed at "Rolleri düzenle" even for a user who may not manage roles (the roles panels need identity.roles.manage) -> that user now reads "rol ataması için yöneticiye başvurun" (M3, M4).
- The button and the create form are on the same tab ("Personel ve roller"); terms are consistent.

## qa-test-kalitesi
- Medium: row text after assigning/revoking a role is not asserted here -> the assignment flow is covered by the existing role-panel tests; not duplicated.
- Low: the negative assert covered the whole row -> narrowed to the role cell, and a user with roles is checked (M2).
- Low: hard-coded sentence not tied to the button name -> accepted; the role-managing case asserts the exact button label text.
