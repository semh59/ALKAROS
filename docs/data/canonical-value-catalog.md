# Canonical Value Catalog

> **Task:** V0-DAT-002
> **Status:** InProgress
> **Assignee:** codex-v0-dat-002
> **Work type:** decision
> **Source basis:** PDF:II.0-II.1, PDF:III.0-III.2, PDF:II.13-II.15, PDF:III.29-III.40, CORR:C2, CORR:C7
> **Date:** 2026-07-30

## 1. Entity Status Values

### Order
| Value | Description |
|-------|-------------|
| `Draft` | Order being composed, not yet submitted |
| `Active` | Order submitted, items being prepared |
| `Closed` | Order completed, bill settled |
| `Cancelled` | Order voided or discarded |

### Bill
| Value | Description |
|-------|-------------|
| `Open` | Bill created, awaiting payment |
| `PartiallyPaid` | Partial payment received |
| `Settled` | Full payment received |
| `Voided` | Bill cancelled |

### Payment
| Value | Description |
|-------|-------------|
| `Pending` | Payment initiated, awaiting authorization |
| `Authorized` | Payment authorized by provider |
| `Captured` | Payment captured successfully |
| `Failed` | Payment failed |
| `Refunded` | Full refund completed |
| `PartiallyRefunded` | Partial refund completed |

### FiscalDocument
| Value | Description |
|-------|-------------|
| `Draft` | Document being prepared |
| `Issued` | Document fiscalized |
| `Cancelled` | Document cancelled |

### ProductionBatch
| Value | Description |
|-------|-------------|
| `Planned` | Batch planned |
| `InProgress` | Production active |
| `Completed` | Production finished |
| `Cancelled` | Batch cancelled |

### PortionReservation
| Value | Description |
|-------|-------------|
| `Reserved` | Portion reserved |
| `Consumed` | Portion used |
| `Released` | Reservation released |
| `Expired` | Reservation timed out |

### KitchenTicket
| Value | Description |
|-------|-------------|
| `Pending` | Ticket created, not yet started |
| `Preparing` | Food being prepared |
| `Ready` | Food ready for service |
| `Served` | Food delivered to table |
| `Cancelled` | Ticket cancelled |

### KitchenTicketItem
| Value | Description |
|-------|-------------|
| `Queued` | Item queued for cooking |
| `Cooking` | Item being cooked |
| `Done` | Item finished |
| `Cancelled` | Item cancelled |

### PrintJob
| Value | Description |
|-------|-------------|
| `Queued` | Job queued for printing |
| `Printing` | Job being printed |
| `Completed` | Print successful |
| `Failed` | Print failed |
| `Cancelled` | Print cancelled |

### CashSession
| Value | Description |
|-------|-------------|
| `Open` | Session active |
| `Closed` | Session ended, awaiting reconciliation |
| `Reconciled` | Session reconciled |

### MealCardSettlement
| Value | Description |
|-------|-------------|
| `Pending` | Settlement pending |
| `Submitted` | Submitted to provider |
| `Settled` | Confirmed by provider |
| `Failed` | Rejected by provider |

### Invoice
| Value | Description |
|-------|-------------|
| `Draft` | Invoice draft |
| `Issued` | Invoice issued |
| `Paid` | Invoice paid |
| `Cancelled` | Invoice cancelled |
| `CreditNote` | Credit note issued |

### ReconciliationCase
| Value | Description |
|-------|-------------|
| `Open` | Case opened |
| `Investigating` | Under investigation |
| `Resolved` | Case resolved |
| `Escalated` | Case escalated |

### Alert
| Value | Description |
|-------|-------------|
| `Active` | Alert active |
| `Acknowledged` | Alert seen by user |
| `Resolved` | Issue resolved |

### Table
| Value | Description |
|-------|-------------|
| `Available` | Table free |
| `Occupied` | Table in use |
| `Reserved` | Table reserved |
| `Cleaning` | Being cleaned |
| `OutOfService` | Table unavailable |

## 2. Discriminator Values

### Payment Method
| Value | Description |
|-------|-------------|
| `Cash` | Cash payment |
| `CreditCard` | Credit/debit card |
| `MealCard` | Meal card / ticket |
| `CustomerAccount` | Charge to account |
| `MobilePayment` | Mobile wallet/QR |

### Tender Type
| Value | Description |
|-------|-------------|
| `Cash` | Physical cash |
| `Card` | Card payment via EFT-POS |
| `MealCard` | Meal card terminal |
| `Account` | Customer account charge |
| `Online` | Online payment gateway |

### Fiscal Device Type
| Value | Description |
|-------|-------------|
| `HuginT300` | Hugin T300 YN ÖKC |
| `QNB` | QNB e-Adisyon |
| `Other` | Other fiscal device |

### Printer Type
| Value | Description |
|-------|-------------|
| `Kitchen` | Kitchen printer |
| `Bar` | Bar printer |
| `Receipt` | Customer receipt printer |
| `Label` | Label printer |

### Actor Type
| Value | Description |
|-------|-------------|
| `Waiter` | Waiter staff |
| `Cashier` | Cashier staff |
| `Manager` | Manager |
| `Kitchen` | Kitchen staff |
| `Host` | Host/seating staff |
| `System` | Automated system action |
| `Customer` | Customer self-service |
| `PaymentProvider` | External payment provider |

## 3. Configuration Status Values

### Printer Route Level
| Value | Description |
|-------|-------------|
| `Item` | Item-level override |
| `Product` | Product-level route |
| `Category` | Category-level route |
| `DailySpecial` | Daily special override |
| `Default` | Default fallback route |

### Reservation Actor
| Value | Description |
|-------|-------------|
| `host` | Host staff |
| `waiter` | Waiter staff |
| `customer_qr` | Customer via QR (read-only) |
| `system` | Automated system |

### Refund Type
| Value | Description |
|-------|-------------|
| `full` | Full refund |
| `partial` | Partial refund |

## 4. Invariants

1. **Single source**: Every status/enum value appears exactly once in this catalog.
2. **No orphan values**: Every value used in the schema MUST be defined here.
3. **No external enum coercion**: Provider-specific external status values are NOT mapped to internal enums.
4. **Backward compatibility**: New values may be added but existing values MUST NOT be removed or renamed.

## 5. Affected Tasks

- GATE-V0-EXIT (catalog completeness check)