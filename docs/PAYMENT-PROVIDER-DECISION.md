# Payment Provider Decision (T401)

**Status:** Selected for implementation (T402-T404). Final commercial contracting, KYC, and
compliance sign-off remain a business/legal step outside this engineering task (see
`docs/PAYMENT-PROVIDER-COMPARISON.md` §4).

## Decision

**Selected provider: Pesapal.**

**Backup/secondary candidate: Flutterwave**, kept as the next option if Pesapal's commercial
terms, KYC requirements, or go-live timeline do not work out during contracting. Because the
codebase integrates providers behind `IPaymentGateway`, switching to the backup later only
requires a new adapter class, not a redesign (see §4).

## Rationale

Weighed against the T400 comparison matrix:

1. **Uganda coverage.** Pesapal has the longest-standing, most Uganda-specific operating history
   of the three aggregators, and is already a familiar checkout experience to Ugandan customers
   (airlines, utilities, and government services use it locally). It supports both required
   methods — card (Visa/Mastercard) and mobile money (MTN, Airtel) — through one merchant
   integration, which is the decisive technical requirement from T400.
2. **Operational support.** Pesapal's East Africa-headquartered support model is the best match
   for a single-country (Uganda) launch: faster local escalation paths, established relationships
   with Ugandan banks and telcos, and no need to navigate a pan-African support structure for a
   problem that is purely local.
3. **Settlement reliability.** Comparable to the other aggregators (T+1 to T+3 typical cycle); no
   material difference found that would favor a different provider.
4. **Developer experience and reconciliation tooling.** Pesapal's integration model (order
   submission -> redirect/IFrame checkout -> IPN callback -> authoritative status query) is
   simpler to reason about and test than DPO's older token/redirect flow, and is not meaningfully
   worse than Flutterwave's more modern SDK-first experience for this platform's needs: a single
   checkout-style payment per booking, not a complex multi-rail payments product. The
   status-query-as-source-of-truth pattern (rather than trusting the IPN callback alone) fits
   cleanly with the existing `IPaymentGateway.CreatePaymentAsync` + a later reconciliation step
   (T403).
5. **Chargeback support.** No material difference found between the three aggregators for this
   platform's card-dispute needs.
6. **Fees.** Not decisive on public information alone (see the comparison document's caveat);
   Pesapal's rates must still be confirmed commercially, but nothing in the public comparison
   suggested materially worse pricing than the alternatives for a Uganda-only merchant.

Flutterwave remains a credible backup because of its broader pan-African reach (useful if the
platform ever expands beyond Uganda), more modern public developer experience, and official SDKs.
It was not selected as primary because this platform is Uganda-only today, and Pesapal's deeper
local presence and support history outweigh Flutterwave's broader (currently unneeded) regional
reach.

DPO Group and the direct-to-telco (MTN/Airtel OpenAPI) option were not selected: DPO offers no
clear advantage over Pesapal for this platform's needs and has a comparatively dated integration
model; the direct-telco option would double the integration/webhook/reconciliation surface (T402,
T403) for mobile money while still requiring a separate card acquirer, which the T400 comparison
found was not justified at this platform's expected transaction volume.

## What T402 should build against

Pesapal API 3.0 (the current Pesapal integration generation) uses this flow, which T402's
concrete `IPaymentGateway` implementation should follow:

1. **Authenticate**: `POST {base}/api/Auth/RequestToken` with the merchant's consumer key/secret
   to obtain a short-lived bearer token.
2. **Register an IPN URL** (one-time per environment, not per transaction):
   `POST {base}/api/URLSetup/RegisterIPN` with this API's public webhook callback URL, returning
   an `ipn_id` to reference when submitting orders.
3. **Submit an order**: `POST {base}/api/Transactions/SubmitOrderRequest` with the booking
   reference (as `id`, which must be unique per attempt), amount, currency, description, callback
   URL, the registered `notification_id` (ipn_id), and billing details. The response includes an
   `order_tracking_id` and a `redirect_url` for the hosted checkout (used as this gateway's
   `CheckoutUrl`).
4. **Receive the IPN callback** (T403): Pesapal calls the registered callback URL with the
   `OrderTrackingId`/`OrderMerchantReference` as query parameters once the transaction reaches a
   terminal or interim state.
5. **Query authoritative status**: `GET {base}/api/Transactions/GetTransactionStatus?orderTrackingId=...`
   — the IPN callback is a prompt to check status, not itself proof of payment; the gateway/
   reconciliation step must call this endpoint to get the authoritative `payment_status_description`
   (e.g. `COMPLETED`, `FAILED`, `INVALID`) before marking a `Payment` as `Succeeded`/`Failed`.

This maps onto the existing abstraction as follows: `CreatePaymentAsync` performs steps 1-3 (token
request if not cached, then order submission) and returns a `PaymentGatewayCreateResult` with
`Status = Pending` and the `redirect_url` as `CheckoutUrl`; the webhook/reconciliation work in
T403 performs steps 4-5 to transition the stored `Payment` to its final status.

## Configuration shape anticipated for T402

Following the existing `GenericHttpPaymentGatewayOptions` pattern
(`PaymentProviders:GenericHttp`), a new `PaymentProviders:Pesapal` options section should hold:

- `Enabled`
- `BaseUrl` (sandbox vs. production host)
- `ConsumerKey` / `ConsumerSecret` (secrets — environment variables only, never committed)
- `CallbackUrl` (this API's public IPN/redirect endpoint)
- `IpnId` (the registered notification id from the one-time IPN registration step, or logic to
  register it automatically on startup if not yet configured)
- `SupportedCurrencies` / `SupportedMethods`, mirroring the generic adapter so `Supports(...)`
  behaves consistently across adapters

## Re-opening this decision

If Pesapal's commercial terms are not acceptable, re-run T401 against Flutterwave using the same
criteria above before considering DPO or the direct-telco option, since this document's comparison
already found no technical reason to prefer either of those two over Pesapal or Flutterwave for
this platform.
