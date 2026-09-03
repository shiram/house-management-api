# HouseHelp Profiles and Media Contract

This document defines the HouseHelp profile fields, visibility boundaries, and authorization rules for Phase 18. It is the implementation contract for T361-T367.

## Goals

- Keep public HouseHelp profiles useful for customers without exposing private worker data.
- Allow HouseHelp users to maintain safe self-service profile fields.
- Keep Manager/Admin users responsible for operational and sensitive profile data.
- Prepare profile-image support without binding the domain model to a specific storage provider.

## Profile field groups

| Field | Purpose | Visibility | Editable by HouseHelp | Editable by Manager/Admin | Notes |
|---|---|---|---:|---:|---|
| `Id` | Stable profile identifier | Public | No | No | Existing field. |
| `UserId` | Optional linked authentication user | Internal | No | Yes | Never exposed publicly. |
| `FirstName` | Display and operations | Public | Limited | Yes | HouseHelp edits should be reviewed or audited. |
| `LastName` | Display and operations | Public | Limited | Yes | Public projection may later support initials/display name if privacy requirements change. |
| `Phone` | Operational contact | Private | Limited | Yes | Never exposed on public endpoints. |
| `City` | Location/search | Public | Yes | Yes | Existing public search field. |
| `Address` | Internal operational address | Private | Limited | Yes | Never exposed on public endpoints. |
| `Bio` | Customer-facing introduction | Public | Yes | Yes | Add in T361. Max length should be constrained. |
| `YearsOfExperience` | Profile credibility | Public | Yes | Yes | Add in T361. Non-negative integer. |
| `Languages` | Customer matching | Public | Yes | Yes | Add in T361. Store as normalized values or child rows if querying becomes important. |
| `EmergencyContactName` | Safety/operations | Private | No | Yes | Add in T361. Do not expose publicly. |
| `EmergencyContactPhone` | Safety/operations | Private | No | Yes | Add in T361. Do not expose publicly. |
| `NationalIdLast4` | Verification reference | Private | No | Yes | Store only the last 4 characters unless a future compliance review approves more. |
| `VerificationStatus` | Worker trust state | Public summary / internal detail | No | Yes | Public value should be coarse: `Unverified`, `Verified`. Internal values may be richer. |
| `ProfileImageStorageKey` | Private storage locator | Internal | Via upload workflow | Via upload workflow | Never return raw storage paths. |
| `ProfileImageUrl` | Safe image access URL | Public if active image exists | Derived | Derived | Returned by API projection, not persisted as an arbitrary user URL. |
| `ProfileImageContentType` | Image validation metadata | Internal | Via upload workflow | Via upload workflow | Allow only approved image MIME types. |
| `ProfileImageSizeBytes` | Image validation metadata | Internal | Via upload workflow | Via upload workflow | Enforce configured max size. |
| `ProfileImageUpdatedAt` | Cache/audit support | Public or internal | Derived | Derived | Safe timestamp. |
| `IsActive` | Operational availability in directory | Public only as filtering effect | No | Yes | Inactive profiles must not appear on public endpoints. |
| `Skills` | Service eligibility | Public | No | Yes | Existing business rule input for assignment. |
| `CreatedAt` | Operational audit metadata | Internal | No | No | Admin/reporting only unless a future public use is approved. |

## API visibility contract

### Public profile projection

Anonymous clients may see only:

- `Id`
- `FirstName`
- `LastName`
- `City`
- `Bio`
- `YearsOfExperience`
- `Languages`
- `VerificationStatus` as a coarse public value
- `ProfileImageUrl`
- `Skills`
- rating/review summary fields already allowed by rating endpoints, if projected later

Public endpoints must only return active HouseHelp profiles. Public responses must never include `UserId`, phone numbers, addresses, emergency contacts, national ID data, storage keys, file paths, or internal audit metadata.

### HouseHelp self profile projection

Authenticated HouseHelp users may see their own:

- public fields
- private contact fields required for their own account/profile maintenance
- image metadata safe for self-service

The profile must be resolved from JWT claims through the linked `HouseHelp.UserId`; clients must not submit arbitrary `UserId` or `HouseHelpId` to access another worker's private profile.

### Manager/Admin profile projection

Manager/Admin users may see and update operational fields:

- linked `UserId`
- phone/address
- skills/service eligibility
- active state
- verification status
- emergency contact fields
- profile image metadata

Manager/Admin endpoints may expose private profile data for operational use, but must still avoid secrets, raw storage paths when unnecessary, and sensitive national ID values beyond the approved last-four representation.

## Authorization rules

| Capability | Anonymous | HouseHelp | Manager | Admin |
|---|---:|---:|---:|---:|
| List active public profiles | Yes | Yes | Yes | Yes |
| View active public profile | Yes | Yes | Yes | Yes |
| View own private profile | No | Own only | Yes | Yes |
| Update own self-service fields | No | Own only | Yes | Yes |
| Update verification/status/skills/UserId | No | No | Yes | Yes |
| Activate/deactivate profile | No | No | Yes | Yes |
| Upload own profile image | No | Own only | Yes | Yes |
| Replace/delete another profile image | No | No | Yes | Yes |
| View raw storage key/path | No | No | No by default | No by default |

All HouseHelp self-service operations must derive identity from JWT claims. Do not trust browser-supplied role, user ID, or profile ownership.

## Image safety rules for T362-T365

- Store uploaded images through a storage abstraction; do not embed provider-specific storage calls in controllers.
- Accept only allow-listed image extensions and MIME types.
- Enforce maximum file size from configuration.
- Generate server-side storage keys; never use client-provided file paths or file names as storage paths.
- Return a safe retrieval URL or API route, not the underlying storage path.
- Replacing an image should orphan/delete the previous image through the storage abstraction where possible.
- Public image retrieval must only serve images for active public profiles.

## Audit expectations

T366 should audit:

- private profile field changes
- verification status changes
- activation/deactivation
- image upload, replacement, and deletion

Audit details must not contain phone numbers, addresses, emergency contact data, national ID fragments, raw image paths, tokens, or provider credentials.

## Deferred decisions

- Whether public display should show full last name or an abbreviated last initial can be revisited if privacy requirements change.
- Whether `Languages` should be a delimited field or normalized child entity depends on future search/filtering needs.
- Full identity-document storage is out of scope and requires a dedicated privacy/compliance review before implementation.
