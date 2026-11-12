# Reviews

## qa-backend
Merged table would lose waiter/party (fixed: primary table's order resolved like the total), stale cancelled order would show an old waiter (fixed: status filter), null-waiter case untested (test added). Performance, authorization, backward compatibility clean.

## qa-api-uclar
Contract additions are trailing optional fields (compatible). Detail endpoint null is documented in the contract comment; accepted because the table plan uses the list. TS model fields (servingWaiterName, partySize) are added in the screen task 999. No OpenAPI file exists.
