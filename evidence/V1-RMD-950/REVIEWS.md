# V1-RMD-950 Reviews

## qa-uctan-uca-akis
Decision valid. The three endpoints return 404 today (cash session without a body, business day and floor plan with the Turkish envelope). Changing them to 200 or 204 breaks the cash session status check (open would be read as open), the cari page, the closing screen and the zone-plan cache, and at least six tests are pinned to 404. The console line is written by the browser for every non-2xx fetch and cannot be silenced from script. Low: the cash 404 has no body while the others do; clients read only the code. Floor plan mapping to 404 was not run.

## qa-api-uclar
Decision valid. 404 is the correct code for a missing resource; 204 would claim the resource exists. The API contract standard asks for a Turkish envelope on 404 and completes a body-less one through the status code pages; no convention test or document forbids 404.
