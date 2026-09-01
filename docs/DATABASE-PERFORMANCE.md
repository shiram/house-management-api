# Database Query Performance Review

## T304 review

Reviewed on 2026-09-01 against EF Core query shapes and entity configurations. The automated integration suite uses EF Core's in-memory provider, so this review does not claim SQL Server execution-plan measurements.

### Current strengths

- Read-heavy service, booking, audit-log, notification, availability, and HouseHelp queries use `AsNoTracking`.
- Booking lookups use the unique booking-reference index; assignment conflict checks use the existing `(AssignedHouseHelpId, ScheduledStart, ScheduledEnd)` index.
- Availability and availability-exception lookup shapes have HouseHelp-scoped composite indexes.
- Notification ownership/unread queries, audit entity/user lookups, and unique natural keys have supporting indexes.
- Booking, audit-log, notification, and service management lists use bounded page sizes where pagination is requested.

### Findings and follow-up

1. Booking lists filter by status, assigned HouseHelp, or client and then order by `CreatedAt`/`Id`. Their current single-column indexes may require sorting at operational scale. Capture SQL Server actual execution plans and row counts before considering targeted composite indexes; do not add overlapping indexes speculatively.
2. Notification lists filter by user and optionally unread state, then order by `CreatedAt`/`Id`. Validate a composite index that matches this exact filter-and-order shape when notification volume warrants it.
3. Audit-log lists use optional action, entity type, and user filters before `CreatedAt`/`Id` ordering. Add a composite index only for a demonstrated high-volume filter shape.
4. HouseHelp filtered pagination has no deterministic ordering and does not cap page size. T306 and T307 own those pagination/filtering corrections.
5. Query performance changes must be measured against SQL Server with representative data, actual execution plans, logical reads, and elapsed time. EF Core in-memory tests cannot validate index usage or query plans.

No migration or index was added by T304 because the current code-level review does not provide production workload evidence for a safe, targeted schema change. T305 separately reviews the booking conflict query.

## T305 booking conflict query review

Reviewed on 2026-09-01. Assignment uses an `AnyAsync`/`EXISTS` overlap check scoped to one assigned HouseHelp:

```text
AssignedHouseHelpId = requestedHouseHelpId
AND BookingId != requestedBookingId
AND status reserves the HouseHelp
AND ScheduledStart < requestedEnd
AND requestedStart < ScheduledEnd
```

The existing `(AssignedHouseHelpId, ScheduledStart, ScheduledEnd)` index aligns with the equality predicate and the first time-range predicate. SQL Server can seek to the selected HouseHelp and constrain candidate bookings by start time; the second time-range condition and status remain residual predicates, which is normal for interval-overlap queries. `AnyAsync` avoids entity materialization and returns once a conflict is found.

The check runs within the existing serializable transaction and per-HouseHelp in-process lock. Those safeguards are preserved because query performance must not weaken double-booking protection.

Before adding a filtered or wider index, capture an actual SQL Server execution plan with representative per-HouseHelp booking history and verify logical reads, duration, lock waits, and index seek/scan behavior. A candidate filtered index must use the same reserving-status definition as the business rule and cannot be added until that definition and workload evidence are validated.

## T306 pagination

All collection queries use a shared default of 50 items per page and enforce a maximum of 100. Callers may request `page` and `pageSize`; absent, zero, or negative values use the safe first-page default. HouseHelp directories now use last name, first name, and ID ordering before pagination so pages are stable. API consumers must request subsequent pages explicitly.
