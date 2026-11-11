# V1-RMD-950 Result

Status: Done (decision, no code change).

- The 404 answers for "no active cash session", "no business day" and "no floor plan" keep their contract. The console line is the browser's own record of a non-2xx answer and cannot be silenced from the page; turning the answers into 200 or 204 would break at least four clients and six tests.
- Open low note: the cash session 404 has no body while the others carry the Turkish envelope; the clients read only the status.
