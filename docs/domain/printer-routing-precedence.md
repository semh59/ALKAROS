# Printer Routing Precedence Contract

> **Task:** V0-DOM-011
> **Status:** Done
> **Assignee:** codex-v0-dom-011
> **Work type:** decision
> **Source basis:** PDF:I.16.1, PDF:II.3.13-II.3.14, CORR:C13
> **Date:** 2026-07-30

## 1. Decision Record

| Field | Value |
|-------|-------|
| **Decision ID** | V0-DOM-011-D001 |
| **Date** | 2026-07-30 |
| **Approver** | TBD |
| **Selected result** | Most-specific-route-wins with explicit fallback chain |
| **Rejected alternatives** | Priority-number system (fragile, requires global coordination); First-match (non-deterministic with overlapping configs) |

## 2. Route Specificity Hierarchy

Routes are evaluated in the following order, from most specific to least specific:

| Level | Scope | Example | Override |
|-------|-------|---------|----------|
| 1 | **Item-level override** | `item_id=abc123 → printer=kitchen-1` | Overrides all below |
| 2 | **Product-level** | `product_id=456 → printer=kitchen-2` | Overrides daily special, category & default |
| 3 | **Daily special override** | `date=2026-07-30, category=specials → printer=kitchen-4` | Overrides category & default for date range |
| 4 | **Category-level** | `category_id=pizza → printer=kitchen-3` | Overrides default |
| 5 | **Default route** | `default → printer=kitchen-main` | Fallback if no match |

## 3. Precedence Rules

### Rule 1: Most Specific Wins
When multiple routes match an item, the most specific level wins. Specificity order: Item > Product > Daily Special > Category > Default.

### Rule 2: Disabled Route
If the winning route's printer is disabled (offline, paper jam, error), the system MUST:
1. Check the next most specific matching route
2. If no alternative match exists, use the default route
3. If default is also disabled, raise `NO_AVAILABLE_PRINTER` error — do NOT print to arbitrary printer

### Rule 3: Ambiguity Rejection
If two routes at the same specificity level match (e.g., two category-level routes for the same item), the configuration is INVALID. The system MUST reject the configuration at validation time.

### Rule 4: Fallback Chain
```
Item route → Product route → Daily Special route → Category route → Default route → NO_AVAILABLE_PRINTER
```

## 4. Configuration Validation Rules

1. **No duplicate routes**: No two routes at the same specificity level may match the same item.
2. **Default must exist**: At least one default printer route MUST be configured.
3. **Printer must exist**: All referenced printers MUST exist in the printer registry.
4. **Circular check**: No route may reference itself indirectly.
5. **Validation on save**: Configuration is validated atomically on save; invalid config is rejected entirely.

## 5. Conflict Examples

### Example 1: Item overrides category
- Item `abc` belongs to category `pizza`
- Category `pizza` → printer `kitchen-3`
- Item `abc` → printer `kitchen-1`
- **Result**: Item `abc` prints to `kitchen-1` (item level wins)

### Example 2: Disabled route fallback
- Item `abc` → printer `kitchen-1` (disabled)
- Category `pizza` → printer `kitchen-3` (available)
- Default → printer `kitchen-main` (available)
- **Result**: Item `abc` prints to `kitchen-3` (next specific match), NOT `kitchen-main`

### Example 3: Ambiguity (configuration error)
- Category `pizza` → printer `kitchen-3`
- Category `pizza` → printer `kitchen-4` (duplicate)
- **Result**: Configuration rejected at validation — `DUPLICATE_ROUTE` error

### Example 4: No available printer
- Item `abc` → printer `kitchen-1` (disabled)
- Category `pizza` → printer `kitchen-3` (disabled)
- Default → printer `kitchen-main` (disabled)
- **Result**: `NO_AVAILABLE_PRINTER` error raised; item queued for manual intervention

## 6. Consumer Task Interface

### Input
```json
{
  "itemId": "uuid",
  "productId": "uuid",
  "categoryId": "uuid",
  "date": "2026-07-30"
}
```

### Output
```json
{
  "printerId": "uuid",
  "routeLevel": "item | product | daily_special | category | default",
  "resolved": true
}
```

### Error Output
```json
{
  "resolved": false,
  "error": "NO_AVAILABLE_PRINTER | AMBIGUOUS_ROUTE | CONFIGURATION_ERROR",
  "details": "string"
}
```

## 7. Affected Tasks

- V1-KIT-002 (Kitchen ticket printer routing)