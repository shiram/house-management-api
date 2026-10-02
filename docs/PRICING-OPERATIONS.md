# Pricing Operations Guide

This is the operational companion to `docs/PRICING-ARCHITECTURE.md`. That document defines the
pricing boundary and terminology; this document shows **how to actually use** the Phase 19
pricing system as a manager/admin, with worked examples, edge cases, and the exact validation
errors clients should expect. It covers T380-T392's implementation as it stands today.

All endpoints below live under `ServicesController` (`/api/services/...`) or
`AdminPublicHolidaysController` (`/api/admin/public-holidays`) unless noted otherwise. All
administration endpoints require `AuthorizationPolicies.ManagerOrAdmin`; quoting and the public
pricing projection are open to anonymous clients.

## 1. Choosing a pricing mode

Every service has exactly one `PricingMode`, set at creation and changed only through
`PUT /api/services/{id}` (changing mode does not retroactively alter existing bookings' snapshots
or existing price rules/time policy rows for the old mode — clean up unused rules/policies when
switching modes).

| Mode | Use for | Configured via |
|---|---|---|
| `Fixed` | A service with one flat price regardless of quantity or duration (e.g. a standard apartment clean). | `Service.BasePrice` |
| `PerUnit` | A service billed by a countable unit (e.g. per room, per kilogram of laundry). | `ServicePriceRule` rows |
| `TimeBased` | A service billed by elapsed time (e.g. hourly cleaning). | `ServiceTimePricingPolicy` |

### 1.1 Fixed pricing example

```http
POST /api/services
{
  "code": "STD_CLEAN",
  "name": "Standard Clean",
  "basePrice": 150.00,
  "pricingMode": "Fixed",
  "isTaxable": true
}
```

A quote or booking for this service always returns one price line:

```json
{ "description": "Standard Clean", "quantity": 1, "unitPrice": 150.00, "lineTotal": 150.00 }
```

**Edge case:** submitting any `pricingItems` to a fixed service's quote/booking request is
rejected with `"This service uses fixed pricing and does not accept pricing items."`.

### 1.2 Per-unit pricing example

```http
POST /api/services/{serviceId}/pricing-rules
{ "unitName": "Room", "unitPrice": 25.00 }

POST /api/services/{serviceId}/pricing-rules
{ "unitName": "Window", "unitPrice": 8.00 }
```

A client requests 3 rooms and 4 windows:

```json
{
  "pricingItems": [
    { "priceRuleId": 1, "quantity": 3 },
    { "priceRuleId": 2, "quantity": 4 }
  ]
}
```

Resulting price lines: `Room x3 = 75.00`, `Window x4 = 32.00`, subtotal `107.00`.

**Edge cases:**

- No pricing items -> `"At least one pricing item is required for this service."`
- The same `priceRuleId` selected twice -> `"Each pricing item can only be selected once."`
- An unknown or deactivated `priceRuleId` -> `"One or more selected pricing items are not available for this service."`
- A quantity of `0` or above `100000` -> `"Each pricing item quantity must be between 1 and 100000."`

Deactivate a rule instead of deleting it (`PUT /api/services/{serviceId}/pricing-rules/{ruleId}/activate?active=false`)
so historical bookings that reference its name/price in their snapshot remain understandable;
the rule itself is never referenced live by an existing booking (see §5).

### 1.3 Time-based pricing example

```http
PUT /api/services/{serviceId}/time-pricing-policy
{
  "billingUnit": "Hour",
  "unitPrice": 20.00,
  "minimumBillableDurationMinutes": 120,
  "billingIncrementMinutes": 30,
  "roundingPolicy": "Up",
  "overtimeThresholdMinutes": 480,
  "overtimeUnitPrice": 30.00
}
```

Calculation flow for a requested window (`ScheduledStart` -> `ScheduledEnd`):

```text
requested minutes (ceil to whole minute)
  -> raised to MinimumBillableDurationMinutes if shorter
  -> snapped to BillingIncrementMinutes per RoundingPolicy (None/Up/Down/Nearest)
  -> split at OvertimeThresholdMinutes into regular + overtime segments
  -> each segment priced at UnitPrice / OvertimeUnitPrice per BillingUnit, rounded to cents
```

Worked example with the policy above: a client requests 50 minutes. Raised to the 120-minute
minimum, then snapped up to the nearest 30-minute increment (already exact) = 120 minutes billed
at `20.00`/hour = **`40.00`**, as a single "Hourly Cleaning (per hour)" line (no overtime, since
120 < 480).

A 9-hour (540-minute) request against the same policy produces two lines: 8 regular hours
(`480 * 20.00 / 60 = 160.00`) and 1 overtime hour (`60 * 30.00 / 60 = 30.00`), total `190.00`.

**Edge cases:**

- No `ServiceTimePricingPolicy` configured for a `TimeBased` service -> `"Time-based pricing is not configured for this service."`
- Any `pricingItems` submitted -> `"This service uses time-based pricing and does not accept pricing items."`
- `OvertimeThresholdMinutes`/`OvertimeUnitPrice` must both be set or both omitted, and the threshold cannot be below `MinimumBillableDurationMinutes` — enforced both by `UpsertTimePricingPolicy`'s 400 response and a DB check constraint.
- Rounding ties under `Nearest` always round **up** (never undercharges on an exact tie).

### 1.4 Schedule validation (all modes)

Every quote/booking, regardless of pricing mode, rejects:

- a `ScheduledStart` that is not in the future -> `"The requested service time must be a future range."`
- a `ScheduledEnd` at or before `ScheduledStart` -> the same message

## 2. Fees and surcharges

Fees and surcharges are composable, optional add-ons layered on top of the base charge
(fixed/per-unit/time-based subtotal). Add them with:

```http
POST /api/services/{serviceId}/fees
{ "name": "Platform Fee", "adjustmentType": "FixedAmount", "amount": 5.00 }

POST /api/services/{serviceId}/surcharges
{ "name": "Weekend Surcharge", "triggerType": "Weekend", "adjustmentType": "Percentage", "amount": 15 }
```

- **Fees** apply whenever `IsActive` is `true` — no trigger condition.
- **Surcharges** apply only when their `TriggerType`'s context matches the requested booking:

| TriggerType | Triggers when | Relevant fields |
|---|---|---|
| `Weekend` | `ScheduledStart` falls on Saturday or Sunday | none |
| `Holiday` | `ScheduledStart`'s date is in the `PublicHoliday` calendar | none (calendar-wide) |
| `AfterHours` | `ScheduledStart`'s minute-of-day falls in the configured window | `AfterHoursStartMinutes`, `AfterHoursEndMinutes` (supports wraparound, e.g. 18:00-08:00) |
| `Urgent` | Lead time from now to `ScheduledStart` is below the threshold | `UrgentLeadTimeMinutes` |
| `Location` | The booking address's `City` or `Region` matches (case-insensitive) | `LocationMatch` |

**Important:** every fee/surcharge percentage is computed against the **original base
subtotal**, not a running total — so multiple fees/surcharges never compound on each other. A
15% weekend surcharge and a 10% urgent surcharge on a `100.00` base both add from `100.00`
(`15.00` + `10.00` = `125.00` total), not `100 * 1.15 * 1.10`.

**Edge cases:**

- An inactive fee/surcharge never applies, even if its trigger would otherwise match.
- A fee/surcharge whose computed amount is `<= 0` (e.g. a fixed amount after a future negative-amount feature) is silently skipped rather than added as a zero line.
- `AdjustmentType.Percentage` amounts must be `(0, 100]`; `FixedAmount` must be positive — enforced as a 400 on create/update, independent of DB check constraints.
- Only the fields relevant to the chosen `TriggerType` are honored; unrelated fields (e.g. `LocationMatch` on a `Weekend` surcharge) are ignored/cleared server-side.

### Public holiday calendar

The `Holiday` surcharge trigger reads a single, service-independent calendar:

```http
POST /api/admin/public-holidays
{ "date": "2026-01-01", "name": "New Year's Day" }

DELETE /api/admin/public-holidays/{id}
```

Only one holiday may exist per date (`409 Conflict` on a duplicate date). The calendar is
queried only when a service actually has an active `Holiday`-trigger surcharge, to avoid an
unnecessary table scan for services that don't use it.

## 3. Tax/VAT and currency

Tax and currency are **not** configured per-service beyond the `Service.IsTaxable` flag. They are
platform-wide settings read through the existing generic settings store:

```http
PUT /api/admin/settings/Pricing.TaxRatePercentage
{ "value": "18" }

PUT /api/admin/settings/Pricing.CurrencyCode
{ "value": "UGX" }
```

- Missing/blank/unparseable `Pricing.TaxRatePercentage` resolves to `0` (no tax); valid values are clamped to `[0, 100]`.
- Missing/blank/implausible `Pricing.CurrencyCode` (must be exactly 3 letters) resolves to `UGX`.
- Tax is computed **after** any promotion discount: `taxableAmount = subtotal - discountAmount`, then rounded to cents. A non-taxable service (`IsTaxable = false`) always shows `TaxRatePercentage = 0` and no tax line, regardless of the platform rate.
- Fees/surcharges apply to the original base subtotal (see §2); tax applies to the subtotal *after* discounts, which is an intentionally different base than fees/surcharges use. Do not conflate the two.

## 4. Public quoting and pricing projection

Anyone (including anonymous clients) can:

- `GET /api/services` / `GET /api/services/{id}` — now include a `TimePricing`, `Fees`,
  `Surcharges`, `TaxRatePercentage`, and `Currency` projection so a client can understand a
  service's charging basis **before** requesting a quote, without needing manager/admin access.
  This projection omits inactive fees/surcharges, internal ids, and audit timestamps.
- `POST /api/services/{id}/quote` — returns a full, receipt-ready breakdown (`PriceLines`,
  `Subtotal`, `TaxRatePercentage`, `TaxAmount`, `Total`, `Currency`, `CalculatedAt`) for a
  candidate schedule/items, without creating a booking. Booking creation (`POST /api/bookings`)
  uses the exact same calculator, so a quote and the resulting booking can never diverge for the
  same inputs.

A quote is advisory only — the server always recalculates from validated inputs at booking
creation time rather than trusting a client-submitted total.

## 5. Immutable booking snapshots

Once a booking is created, its price lines, `TotalPrice`, `TaxRatePercentage`, `TaxAmount`, and
`Currency` are **permanently frozen**, regardless of any later change to the service's base
price, price rules, time policy, fees, surcharges, or tax/currency settings. This is because:

- `BookingPriceLine` stores only copied `Description`/`Quantity`/`UnitPrice`/`LineTotal` values — it has **no foreign key** back to `ServicePriceRule`/`ServiceFee`/`ServiceSurcharge`/`ServiceTimePricingPolicy`.
- `IPricingCalculationService.Calculate` is invoked only from booking creation, repeat-booking creation, and the quote endpoint — never from booking retrieval, assignment, status transitions, or reporting.

Operationally: if you change a price rule's rate, every **existing** booking that used the old
rate is unaffected; only **new** quotes/bookings reflect the change. There is currently no
"repricing" workflow for existing bookings — if one is ever introduced, it must be explicit,
authorized, audited, and must preserve the prior snapshot rather than overwrite it (per
`docs/PRICING-ARCHITECTURE.md`).

## 6. Pricing versions (effective-dated changes)

`ServicePricingVersion` lets a manager schedule a **future** price change without touching the
live catalog today:

```http
POST /api/services/{serviceId}/pricing-versions
{ "effectiveFrom": "2026-06-01T00:00:00Z", "basePrice": 175.00 }

POST /api/services/pricing-versions/{versionId}/publish
```

- A draft is validated against the service's *current* `PricingMode` (fixed needs `BasePrice`; per-unit needs at least one valid, non-duplicate `Units` entry; time-based needs a complete, consistent duration/overtime policy).
- Publishing a version transactionally closes the previously open published version's `EffectiveTo` at the new version's `EffectiveFrom`, so at most one published version is ever open per service.
- Publishing a draft whose `EffectiveFrom` would overlap the currently open published version is rejected with a `409` and a descriptive error.
- `GetEffectiveVersionAsync(serviceId, atUtc)` resolves the single version covering a given instant, using the *requested service time*, not the HTTP request time.

**Known limitation (tracked, not a bug to "fix" under T392/T393):** as of this writing,
`PricingCalculationService.Calculate` still reads the **live** `Service.BasePrice`/`ServicePriceRule`/
`ServiceTimePricingPolicy` tables for every pricing mode — it does not yet resolve or consume a
published `ServicePricingVersion`. Publishing a pricing version today records an auditable,
effective-dated intent and keeps the version history consistent, but it does **not** itself change
what a quote or booking is charged; that still happens by directly editing the live price
rule/base price/time policy (§1) through their own admin endpoints. Wiring live calculation to
resolve the effective version at the requested service time remains open future work and should
be scoped as its own task before it is advertised to admins as changing live pricing.

## 7. Audit trail

Every pricing administration mutation writes an `AuditLog` row (`GET /api/admin/audit-logs`,
`AuthorizationPolicies.AdminOnly`) with the acting user's id (from JWT claims, never trusted from
the request body), the affected entity type/id, and a concise, non-sensitive details string (e.g.
`"ServiceId: 4, UnitName: Room, UnitPrice: 30"` or `"IsActive: False"`):

| Action | Fires on |
|---|---|
| `service_price_rule.created` / `.updated` / `.activation_changed` | Price rule create/update/activate-deactivate |
| `service_time_pricing_policy.updated` | Time pricing policy upsert |
| `service_fee.created` / `.updated` / `.activation_changed` | Fee create/update/activate-deactivate |
| `service_surcharge.created` / `.updated` / `.activation_changed` | Surcharge create/update/activate-deactivate |
| `service_pricing_version.created` / `.published` | Pricing version draft create / publish |
| `public_holiday.created` / `.deleted` | Public holiday create/delete |

These audit events never touch, reference, or alter an existing booking's price snapshot (§5) —
they only record changes to the live catalog used for future quotes/bookings.

## 8. Quick reference: endpoints and authorization

| Endpoint | Method | Auth |
|---|---|---|
| `/api/services` | GET | Public (active only) |
| `/api/admin/services` | GET/POST/PUT | Manager/Admin (all services, including inactive) |
| `/api/services/{id}` | GET | Public (active) |
| `/api/services/{id}/quote` | POST | Public |
| `/api/services/{id}/pricing-rules` | GET/POST | Manager/Admin |
| `/api/services/{id}/pricing-rules/{ruleId}` | PUT | Manager/Admin |
| `/api/services/{id}/pricing-rules/{ruleId}/activate` | PUT | Manager/Admin |
| `/api/services/{id}/time-pricing-policy` | PUT | Manager/Admin |
| `/api/services/{id}/fees` | GET/POST | Manager/Admin |
| `/api/services/{id}/fees/{feeId}` | PUT | Manager/Admin |
| `/api/services/{id}/fees/{feeId}/activate` | PUT | Manager/Admin |
| `/api/services/{id}/surcharges` | GET/POST | Manager/Admin |
| `/api/services/{id}/surcharges/{surchargeId}` | PUT | Manager/Admin |
| `/api/services/{id}/surcharges/{surchargeId}/activate` | PUT | Manager/Admin |
| `/api/services/{id}/pricing-versions` | GET/POST | Manager/Admin |
| `/api/services/pricing-versions/{versionId}/publish` | POST | Manager/Admin |
| `/api/admin/public-holidays` | GET/POST | Manager/Admin |
| `/api/admin/public-holidays/{id}` | DELETE | Manager/Admin |
| `/api/admin/settings/Pricing.TaxRatePercentage`, `.../Pricing.CurrencyCode` | GET/PUT | Admin only |
| `/api/admin/audit-logs` | GET | Admin only |
