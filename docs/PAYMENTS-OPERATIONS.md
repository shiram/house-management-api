# Payments Operations Guide

This is the operational companion to `docs/PAYMENT-PROVIDER-COMPARISON.md` (provider evaluation,
T400) and `docs/PAYMENT-PROVIDER-DECISION.md` (Pesapal selection and integration contract, T401).
Those documents explain **why** Pesapal was chosen and **what** its API contract looks like; this
document explains **how to operate** the integration as it stands today (T402-T404): sandbox vs.
production configuration, the one-time IPN registration step, how reconciliation/idempotency
works, how to validate the integration without real credentials, and what to check when a payment
appears stuck.

## 1. Configuration: sandbox vs. production

The Pesapal adapter (`PesapalPaymentGateway`) is entirely configuration-driven and ships
**disabled** by default. It is configured exclusively through environment variables or secret
stores — never through values committed to `appsettings.json` — per `PaymentProviders:Pesapal`:

| Setting | Purpose |
|---|---|
| `PaymentProviders__Pesapal__Enabled` | Must be `true` for the gateway to be selectable at all (`Supports()` returns `false` otherwise). |
| `PaymentProviders__Pesapal__BaseUrl` | `https://cybqa.pesapal.com/pesapalv3` (sandbox) or `https://pay.pesapal.com/v3` (production). |
| `PaymentProviders__Pesapal__ConsumerKey` / `PaymentProviders__Pesapal__ConsumerSecret` | Merchant credentials issued by Pesapal. **Never commit these.** |
| `PaymentProviders__Pesapal__CallbackUrl` | This API's own public IPN endpoint, `https://<host>/api/payments/webhooks/pesapal`. Must be reachable from the public internet for both sandbox and production — Pesapal calls back from its own servers, not from the customer's browser. |
| `PaymentProviders__Pesapal__IpnId` | The `notification_id` returned by a one-time `RegisterIPN` call (see §2). |

Switching from sandbox to production is **only** a matter of swapping these five values in the
deployment's environment/secret store; no code or redeploy-specific logic exists for the
distinction. Treat sandbox and production `ConsumerKey`/`ConsumerSecret`/`IpnId` values as
entirely separate secrets — a sandbox IPN registration is not valid against the production base
URL and vice versa.

If `Enabled` is `false` (or any of `BaseUrl`/`ConsumerKey`/`ConsumerSecret`/`CallbackUrl`/`IpnId`
is blank), `Supports()` returns `false` for every request and `PaymentService` falls back to the
`GenericHttpPaymentGateway` (intended for local/throwaway HTTP stub testing only — never enable
both gateways for the same currency/method combination in production).

## 2. One-time setup: registering the IPN callback

Before any payment can be reconciled, Pesapal must be told where to send IPN notifications. This
is a **manual, one-time (per environment) operation**, not something this API performs
automatically on startup:

1. Obtain a bearer token via `POST {BaseUrl}/api/Auth/RequestToken` with the configured
   `ConsumerKey`/`ConsumerSecret`.
2. Call `POST {BaseUrl}/api/URLSetup/RegisterIPN` with `{ "url": "<CallbackUrl>", "ipn_notification_type": "GET" }`
   (this API's callback is a `GET` endpoint — see §3 — so the notification type must match).
3. Pesapal returns an `ipn_id` in the response. Set `PaymentProviders__Pesapal__IpnId` to that
   value for the environment you just registered.
4. Repeat separately for sandbox and production — they are registered against different base
   URLs and are never interchangeable.

Re-registering is required whenever `CallbackUrl` changes (e.g. a new public hostname after a
redeploy to a different domain).

## 3. The IPN callback and reconciliation design

`PaymentWebhooksController` exposes `GET /api/payments/webhooks/pesapal`
(`[AllowAnonymous]`, rate-limited — see §5), accepting Pesapal's documented query parameters:
`OrderTrackingId`, `OrderMerchantReference`, `OrderNotificationType`.

**Core design rule: the callback is only ever a trigger, never evidence.** Pesapal's IPN v3
contract carries no cryptographic signature, so this API never treats the callback's own query
parameters as proof of a payment outcome. Instead, on every callback:

1. The `Payment` row matching `(ProviderName = "pesapal", ProviderReference = OrderTrackingId)` is
   looked up. An unknown tracking id is acknowledged (`200 OK`) without further action — Pesapal
   must never receive an error for a tracking id it legitimately sent, and this API does not leak
   whether a payment exists.
2. If that payment is already in a **terminal** state (`Succeeded`/`Failed`/`Cancelled`/`Refunded`),
   the callback is a no-op: no provider re-query is made. Pesapal explicitly documents it may
   deliver the same IPN more than once, and reconciliation is therefore idempotent by design, not
   by accident.
3. Otherwise, `PesapalPaymentGateway.GetStatusAsync` makes an **authenticated, server-to-server**
   call to `GET {BaseUrl}/api/Transactions/GetTransactionStatus` using this API's own cached
   bearer token — never a value supplied by the caller — and that response is the only thing ever
   trusted to update `Payment.Status`.
4. A status change is recorded as an `AuditLog` row (`AuditEventTypes.PaymentStatusReconciled`,
   `userId: null` since no authenticated user initiates a provider callback) so every transition
   has a paper trail independent of application logs.

Any exception during reconciliation (e.g. the provider is briefly unreachable) is logged and
**swallowed** — the endpoint still returns `200 OK` so Pesapal does not enter an aggressive retry
loop for a condition this API cannot resolve synchronously. A failed reconciliation attempt is
safe to retry later (manually, or via the next replayed IPN); nothing is lost because the
authoritative state lives at Pesapal, not in the callback payload.

**Response shape:** the callback returns `JsonResult` (not `Ok(...)`), deliberately bypassing the
application-wide `ApiResultFilter` response envelope (`{statusCode, message, data}`) that wraps
every other controller's `ObjectResult`. Pesapal's IPN client expects the flat
`{orderNotificationType, orderTrackingId, orderMerchantReference, status}` shape at the top level,
not nested under `data` — wrapping it would silently break Pesapal's own acknowledgement parsing.
Do not change this controller back to `Ok(...)` without re-confirming Pesapal's integration still
works; this was caught by the sandbox integration tests in §4, not by unit tests (which assert on
the returned `IActionResult` directly and never exercise the real MVC filter pipeline).

## 4. Validating the integration without real credentials

`PesapalSandboxIntegrationTests.cs` exercises the entire integration end-to-end through the real
HTTP pipeline (DI, options binding, rate limiting, controller routing, the `ApiResultFilter`) —
**without** any real Pesapal sandbox account. A stub `HttpMessageHandler` is bound to the
application's own `PesapalPaymentGateway` typed `HttpClient`, standing in for Pesapal's
`RequestToken`/`SubmitOrderRequest`/`GetTransactionStatus` endpoints. The
`ConsumerKey`/`ConsumerSecret`/`IpnId` values used are clearly-fake placeholders
(`sandbox-placeholder-...`), never real secrets.

Covered scenarios:

- A callback reconciles a `Pending` payment to `Succeeded` (`payment_status_description: COMPLETED`).
- A callback reconciles a `Pending` payment to `Failed`, capturing the provider's failure message.
- A replayed callback against an already-`Succeeded` payment never re-queries the provider
  (`GetTransactionStatus` call count is asserted unchanged across the replay).
- An unknown `OrderTrackingId` is acknowledged with `200 OK` and causes no database change and no
  provider status query.
- A callback missing `OrderTrackingId` is rejected with `400 Bad Request` before any reconciliation
  is attempted.

Run just this suite locally with:

```bash
dotnet test src/HouseManagement.Api.Tests/HouseManagement.Api.Tests.csproj --filter "FullyQualifiedName~PesapalSandboxIntegrationTests"
```

Use this suite (rather than a manual call against Pesapal's real sandbox) as the first check after
any change to `PesapalPaymentGateway`, `PaymentReconciliationService`, or
`PaymentWebhooksController` — it catches contract regressions (such as the response-envelope issue
in §3) that provider-facing manual testing would only surface much later, against a live sandbox
account.

## 5. Rate limiting

The callback endpoint is throttled independently of other API rate limit policies via
`RateLimitPolicyNames.PaymentWebhook`, configured under `RateLimiting:PaymentWebhook` (defaults:
60 requests / 60 seconds, fixed window, partitioned per client IP). Tune via
`RateLimiting__PaymentWebhook__PermitLimit` / `RateLimiting__PaymentWebhook__WindowSeconds` if
Pesapal's retry behavior in a given environment needs a wider or narrower allowance. This endpoint
is anonymous by necessity (Pesapal calls it directly, with no session), so the rate limit is this
API's primary defense against abuse of the endpoint, separate from the idempotency/reconciliation
design in §3, which defends against **trust**, not **volume**.

## 6. Troubleshooting: a payment stuck in `Pending`/`Processing`

1. **Confirm the IPN was actually registered against the `CallbackUrl` currently configured** —
   see §2. A stale registration (e.g. after a hostname change) means Pesapal never calls back at
   all, and the payment will never move out of `Pending` on its own.
2. **Check the rate limit wasn't exhausted** — if Pesapal is retrying aggressively and the window
   policy (§5) is too tight for the current traffic pattern, legitimate callbacks may be getting
   `429`'d. Check logs for `AuditEventTypes.PaymentStatusReconciled` absence combined with
   elevated `429` responses on `/api/payments/webhooks/pesapal`.
3. **Manually trigger reconciliation** — reconciliation is also safely callable directly (it is a
   plain `IPaymentReconciliationService.ReconcileAsync(providerName, providerReference)` call) from
   an operator tool/script/REPL against the running application, since it always re-queries the
   provider rather than trusting any cached/stale value. This is the safe way to "nudge" a stuck
   payment without needing to wait for or fake another IPN callback.
4. **Check `Payment.FailureReason`** — if the payment did transition to `Failed` but that wasn't
   expected, the provider's own message (captured verbatim from `GetTransactionStatus`) is stored
   there and is the fastest way to understand why, without needing provider dashboard access.
5. **Remember payment initiation is a separate, currently-unexposed step** — as of this writing
   there is no public `POST /api/bookings/{id}/payments` (or similar) HTTP endpoint;
   `IPaymentService.InitiateAsync` has no caller outside of tests. If a payment is unexpectedly
   absent entirely (not merely `Pending`), this is the likely reason, not a reconciliation defect.
   Adding that endpoint is tracked as separate, out-of-scope follow-up work (originally a Phase 17
   gap), not part of Phase 20's webhook/reconciliation scope.

## 7. Quick reference: endpoints and authorization

| Endpoint | Method | Auth |
|---|---|---|
| `/api/payments/webhooks/pesapal` | GET | Anonymous (provider-to-server callback; rate-limited, never trusted alone — see §3) |

No manager/admin-facing payment endpoints exist yet (see §6, item 5); this table will grow once a
payment initiation/administration surface is added.
