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
