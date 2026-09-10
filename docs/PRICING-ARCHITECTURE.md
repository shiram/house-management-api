# Pricing Architecture and Terminology

This document defines the pricing boundary for Phase 19. It preserves the current fixed and per-unit behavior while establishing the concepts and ownership rules needed for time-based pricing, effective-dated rates, quotes, fees, taxes, administration, and immutable booking snapshots.

## Goals

- Keep pricing rules inside the API and enforce them server-side.
- Support fixed, per-unit, and time-based services through one coherent calculation flow.
- Let managers schedule future price changes without altering historical bookings.
- Give public clients an understandable quote before booking submission.
- Preserve the exact accepted price on the booking independently of later catalog changes.
- Keep pricing independent from payment providers and payment collection.

## Current implementation

The existing implementation remains valid and must be extended incrementally:

- `Service.PricingMode` supports `Fixed`, `PerUnit`, and `TimeBased`. Time-based services cannot be booked until their duration policy is introduced by T382.
- `Service.BasePrice` is the fixed-service amount.
- `ServicePriceRule` stores active per-unit names and unit prices.
- Booking requests submit `PricingItems` containing a price-rule identifier and integer quantity.
- `BookingService` calculates pricing during booking creation.
- `BookingPriceLine` stores description, quantity, unit price, and line total without a foreign key to a mutable price rule.
- `Booking.TotalPrice`, promotion fields, and the negative promotion price line form the accepted booking snapshot.
- Payment initiation reads `Booking.TotalPrice`; payment providers do not calculate service prices.

Current limitations:

- Pricing rules are mutable and are not effective-dated.
- There is no duration billing policy for the time-based pricing mode.
- Quote calculation is private to booking creation; there is no public quote endpoint.
- There are no service fees, surcharges, tax lines, or persisted currency on the booking snapshot.
- Per-unit quantities are whole numbers only.

## Module boundary

Pricing belongs to the service catalog business module, with a dedicated pricing slice:

```text
Features/
  Services/
    Pricing/
      Definitions
      Administration
      Quotes
      Calculation
      Snapshots
```

This remains part of the modular monolith. It is not a payment service and must not call a card, mobile-money, or other payment provider.

### Pricing owns

- pricing modes and billable units
- effective-dated price definitions and versions
- fixed, per-unit, and time-based rates
- minimums, rounding, overtime, fees, and surcharges
- quote input validation
- deterministic server-side calculation
- receipt-ready price breakdowns
- creation of immutable booking price snapshots
- pricing administration authorization and audit events

### Service catalog owns

- service identity, code, name, description, and active state
- association between a service and its pricing definitions
- public service availability

### Booking owns

- requested service, schedule, address, and customer context
- the accepted immutable price snapshot
- promotion references and discount snapshot
- booking lifecycle after creation

Booking creation may invoke the pricing calculator, but must not duplicate pricing formulas.

### Promotions own

- promotion eligibility
- active windows and usage limits
- percentage or fixed discount calculation

Promotions consume a pricing subtotal and return a discount adjustment. They do not define the underlying service rate.

### Payments own

- collection of the booking amount
- provider initiation, reconciliation, status, and idempotency
- payment method and provider references

Payments consume the final immutable booking amount and currency. They must never recalculate service pricing or modify price lines.

## Terminology

| Term | Definition |
|---|---|
| **Pricing mode** | The primary charging method for a service: fixed, per-unit, or time-based. |
| **Price definition** | The manager-controlled configuration describing how a service can be charged. |
| **Pricing version** | An immutable, effective-dated revision of a price definition used to resolve the correct rules for a requested service time. |
| **Rate** | A monetary amount applied to a fixed service, unit, or duration unit. |
| **Billable unit** | The measurable basis for a charge, such as item, room, kilogram, hour, or 30-minute block. |
| **Quantity** | The validated number of billable units selected or derived for a quote. |
| **Requested duration** | The elapsed time between the requested booking start and end. |
| **Billable duration** | The duration after minimums and rounding policies are applied. |
| **Base charge** | The primary fixed, per-unit, or time-based service charge before adjustments. |
| **Fee** | A generally applicable additional charge, such as a service or platform fee. |
| **Surcharge** | A conditional additional charge caused by context such as urgency, location, weekend, holiday, or after-hours work. |
| **Discount** | A promotion-derived reduction represented as a negative adjustment. |
| **Tax** | A statutory amount calculated according to the tax policy defined in T387. |
| **Quote** | A server-calculated, non-payment representation of the expected charges for validated inputs at a point in time. |
| **Price line** | One receipt-ready component containing type, description, quantity or duration, rate, and amount. |
| **Subtotal** | The sum of positive service charges, fees, and surcharges before discounts and taxes, subject to the policy finalized in T387. |
| **Total** | The final amount represented by the accepted snapshot and collected by payments. |
| **Price snapshot** | The immutable pricing data copied onto a booking when the request is accepted. |

Use `decimal` with explicit SQL precision for every monetary value. Do not use `float` or `double`.

## Pricing modes

### Fixed

A fixed service has one base amount for the requested service. It does not accept per-unit pricing items.

Current compatibility:

- `Service.BasePrice` remains the fixed amount until effective-dated versions replace it.
- The snapshot contains one service line with quantity `1`.

### Per-unit

A per-unit service charges one or more selected units:

```text
line amount = validated quantity * unit rate
```

Examples include kilograms of laundry, rooms, windows, or items.

Current compatibility:

- Existing `ServicePriceRule` rows remain the source of unit names and rates until migrated into versioned definitions.
- Existing integer quantities remain supported.
- Duplicate selections and inactive or foreign price rules remain invalid.

### Time-based

A time-based service derives billable quantity from the requested duration and the configured duration unit:

```text
requested duration
  -> minimum billable duration
  -> rounding policy
  -> regular/overtime allocation
  -> billable duration units
  -> rate calculation
```

T382 will define the persistence and exact calculation fields. Until then, booking creation rejects time-based services with an explicit configuration error. Time-based pricing must not infer policy solely from the booking UI or from an unversioned global setting.

## Pricing resolution and calculation flow

The server-side flow is:

```text
service + requested schedule + pricing inputs + address/context
  -> validate service is active
  -> resolve pricing version effective at the requested service time
  -> validate inputs for the resolved pricing mode
  -> calculate base charge
  -> calculate applicable fees and surcharges
  -> calculate promotion discount
  -> calculate tax according to the configured tax policy
  -> return quote breakdown and total
  -> on booking acceptance, copy the complete breakdown into the booking snapshot
```

The calculator must be deterministic for the same pricing version, request inputs, and calculation timestamp. It must return explicit validation or business errors rather than silently falling back to zero, another pricing mode, or a stale rate.

## Effective-dated pricing

Future pricing versions will use an explicit effective window:

- `EffectiveFrom` is required and stored as UTC `DateTimeOffset`.
- `EffectiveTo` is optional and exclusive.
- At most one published pricing version may be effective for a service at a given instant.
- Draft versions are not available to public quoting or booking.
- Published versions are immutable; corrections create a new version.
- Resolution uses the requested service start time, not the HTTP request time.
- A service with no valid effective pricing version cannot be quoted or booked.

Existing unversioned fixed/per-unit data must be migrated into an initial pricing version without changing existing booking snapshots.

## Quote contract

A quote is advisory until accepted through booking creation. It should include:

- service and pricing-version identifiers
- pricing mode and currency
- normalized billable inputs
- requested and billable duration where applicable
- itemized base charges, fees, surcharges, discounts, and taxes
- subtotal, discount total, tax total, and final total
- calculation timestamp
- expiry or revalidation requirement

The client must not submit calculated totals as authoritative values. Booking creation resubmits the business inputs or a tamper-resistant quote reference and the server recalculates or validates the quote before persisting the snapshot.

## Immutable booking snapshot

Once a booking is accepted:

- price lines and aggregate amounts are owned by the booking
- later service, rate, fee, surcharge, promotion, or tax changes do not alter the booking
- snapshot lines must not require mutable catalog rows to render a receipt
- the snapshot records the pricing version and currency used
- line descriptions, quantities, duration details, rates, and amounts are copied values
- recalculation is not performed during assignment, confirmation, completion, reporting, or payment

An intentional repricing workflow, if introduced later, must be explicit, authorized, audited, and preserve the prior snapshot rather than silently overwriting it.

## Calculation invariants

- Only active services with a valid published pricing version may be quoted or booked.
- Inputs must match the pricing mode; fixed services reject item quantities, and per-unit services reject unknown or inactive units.
- Time-based pricing uses server-derived duration from validated timestamps.
- Negative rates and quantities are invalid.
- Duplicate unit selections are invalid unless a future definition explicitly models tiers.
- Intermediate and final values must fit the configured decimal precision.
- Rounding occurs at documented boundaries and uses one configured monetary rounding rule.
- The final total must equal the sum of persisted price lines.
- Discounts cannot reduce the payable amount below the policy-defined floor.
- Currency must be a supported ISO 4217 code and must not be inferred by a payment provider.
- Price definitions and versions use database constraints for important uniqueness and effective-window invariants.

## API and authorization boundaries

Public/anonymous clients may:

- view safe pricing explanations for active services
- request a quote
- submit quote inputs during anonymous booking creation

Manager/Admin users may:

- create draft pricing versions
- configure rates, duration policies, fees, and surcharges
- publish or retire pricing versions according to valid effective-date rules

HouseHelp users do not administer service pricing. Internal cost, earnings, or payout calculations are separate from the client-facing service price.

API responses use pricing-specific DTOs and never expose EF entities. Public projections must explain the charging basis without exposing internal audit metadata or unrestricted administrative rules.

## Operational and concurrency rules

- Publishing a version and closing the prior effective window must be transactional.
- Concurrent publication must not create overlapping effective versions.
- Booking creation must use one resolved version throughout calculation and snapshot persistence.
- Promotion usage and booking snapshot persistence remain transactional where usage limits apply.
- Reporting reads booking snapshots, not current catalog prices.
- Pricing audit events record identifiers and changed field names or non-sensitive values; they must not contain customer contact data or payment credentials.

## Phase 19 implementation sequence

| Task | Boundary established by this document |
|---|---|
| T381-T382 | Add time-based mode and duration-policy persistence without changing existing fixed/per-unit semantics. |
| T383 | Introduce immutable effective-dated pricing versions and migrate current definitions. |
| T384-T385 | Extract deterministic quote calculation and validate all pricing inputs server-side. |
| T386 | Add composable fees and contextual surcharges. |
| T387 | Finalize currency, tax/VAT basis, rounding, and receipt totals. |
| T388-T389 | Add authorized administration and safe public projections. |
| T390 | Complete immutable snapshot metadata and invariants. |
| T391 | Add stable pricing audit event types and coverage. |
| T392 | Verify every pricing mode, adjustment, effective-date, concurrency, and snapshot path. |
| T393 | Publish the operational runbook and worked examples. |

## Out of scope

- provider-specific card or mobile-money behavior
- settlement, refunds, chargebacks, and payout calculations
- dynamic market pricing or machine-learning price selection
- client-authored formulas
- silent repricing of existing bookings
- multiple deployable pricing microservices
