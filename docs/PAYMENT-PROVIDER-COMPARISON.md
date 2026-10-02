# Payment Provider Comparison (T400)

This is the research input for **T401** (production provider selection). It compares the
candidates named in `TASKS.md` for Uganda card (Visa/Mastercard) and mobile-money (MTN Mobile
Money, Airtel Money) coverage against the criteria T401 will decide on: Uganda coverage,
settlement reliability, fees, chargeback support, reconciliation tooling, developer experience,
and operational support. It does not select a provider — that is T401's decision, to be made with
commercial/legal input alongside this technical comparison.

Today the platform integrates against a **generic, provider-neutral HTTP sandbox adapter**
(`GenericHttpPaymentGateway`, Phase 17/T347) so that whichever provider is selected in T401 can be
wired in through the same `IPaymentGateway` contract without changing `PaymentService`, the
`Payment` model, or the booking/pricing flow that depends on it.

## 1. Candidates considered

| Candidate | What it is |
|---|---|
| **Flutterwave** | Pan-African payment aggregator (Nigeria-headquartered) offering card, mobile money, and bank transfer rails across many African markets including Uganda, via a single merchant integration and dashboard. |
| **Pesapal** | East Africa-focused payment aggregator (Kenya-headquartered, long-standing Uganda presence) offering card and regional mobile money collection, with an IFrame/hosted-checkout-first integration model. |
| **DPO Group (Direct Pay Online)** | Pan-African payment aggregator (South Africa-headquartered, part of Network International) with a long operating history in East Africa, offering card and mobile money collection through a token/redirect checkout flow. |
| **Direct MTN MoMo API + Airtel Money OpenAPI** ("locally preferred" alternative) | Integrating directly against MTN Mobile Money's Open API (Collection/Disbursement products) and Airtel Money's OpenAPI, bypassing an aggregator for mobile-money traffic. Card acceptance would still require a separate acquirer/aggregator (e.g. one of the above) since neither telco offers card acquiring. |

All four are plausible for this platform's volume profile (anonymous and authenticated customers
booking home services, single-item checkouts, UGX-denominated). None are ruled out by this
document; T401 should also validate current commercial terms directly with each provider, since
fee schedules and settlement terms are typically negotiated per merchant and are not fully public.

## 2. Comparison matrix

| Criterion | Flutterwave | Pesapal | DPO Group | Direct MTN/Airtel |
|---|---|---|---|---|
| **Uganda card coverage** | Visa/Mastercard via hosted checkout or API, widely used across Africa including Uganda. | Visa/Mastercard via hosted checkout, long-established in Uganda specifically. | Visa/Mastercard via hosted/token checkout, long-established in East Africa. | None — telcos do not acquire cards; would require a second provider for card. |
| **MTN Mobile Money (Uganda)** | Supported as a collection method through the aggregator's unified API. | Supported as a collection method through the aggregator's unified API. | Supported as a collection method through the aggregator's unified API. | Native, first-party integration against MTN's own Collection API — no aggregator markup, but MTN merchant/API onboarding (sandbox + production) is required directly with MTN. |
| **Airtel Money (Uganda)** | Supported as a collection method through the aggregator's unified API. | Supported, though historically Pesapal's strongest telco relationships in the region have been MTN/Safaricom-adjacent; Airtel coverage should be confirmed for Uganda specifically before relying on it. | Supported as a collection method through the aggregator's unified API. | Native, first-party integration against Airtel's OpenAPI — separate onboarding/credentials from MTN. |
| **Settlement reliability/speed** | T+1 to T+3 typical for aggregator settlement to a local bank account; varies by payout schedule agreed at onboarding. | Established Uganda settlement track record; typical aggregator T+1 to T+3 cycle. | Established East Africa settlement track record; typical aggregator T+1 to T+3 cycle. | MTN/Airtel wallet settlement can be same-day to the linked merchant wallet/bank, but requires the business to separately manage two telco relationships and two settlement cycles instead of one consolidated one. |
| **Fees** | Aggregator transaction fee (percentage + possible fixed fee), varies by method and negotiated merchant tier. Needs direct confirmation from Flutterwave's Uganda/current rate card — not reliably published. | Aggregator transaction fee, varies by method and merchant tier; historically competitive for Uganda-specific mobile money. Needs direct confirmation from current rate card. | Aggregator transaction fee, varies by method and merchant tier. Needs direct confirmation from current rate card. | No aggregator markup on mobile-money legs, but still bears whatever direct MTN/Airtel merchant fee structure applies, plus the cost of maintaining two direct integrations and reconciliation processes instead of one. Still needs a separate card acquirer, which reintroduces an aggregator fee for that leg. |
| **Chargeback support (cards)** | Aggregator-managed dispute/chargeback workflow with dashboard visibility and API webhooks for status changes. | Aggregator-managed dispute/chargeback workflow with dashboard visibility. | Aggregator-managed dispute/chargeback workflow with dashboard visibility; DPO has a long card-scheme compliance history in the region. | Not applicable to mobile money (no chargeback scheme equivalent); would still depend on whichever card acquirer is paired with it. |
| **Reconciliation tooling** | Dashboard + transaction/settlement reports + webhooks; supports idempotency keys and transaction verification endpoints. | Dashboard + transaction reports; supports a server-to-server transaction status/verification call model (IPN - Instant Payment Notification). | Dashboard + transaction reports; token-based verification call to confirm a transaction's final status. | No unified dashboard across both telcos; reconciliation means combining two separate transaction/status feeds (plus a third for the paired card acquirer), which is materially more reconciliation engineering than a single aggregator. |
| **Developer experience** | Modern REST API, official SDKs, detailed public developer docs, sandbox environment, active developer community/support channels. | REST/IFrame-based API, public developer docs and sandbox, smaller SDK ecosystem than Flutterwave but long operating history with Uganda-based integrators. | REST/token API, public developer docs and sandbox; integration patterns are a little more dated (redirect/token-first) than Flutterwave's. | Two separate, partner-grade API programs (MTN Open API, Airtel OpenAPI) with their own sandbox onboarding, documentation quality, and support SLAs that are generally less polished and less consistent than a dedicated aggregator's. |
| **Operational support** | Regional support presence, Africa-wide; Uganda-specific account support depends on current local partnerships. | Strong Uganda-specific operational history and local support relationships (East Africa-headquartered, long local presence). | Established East Africa support presence, part of a larger international payments group (Network International) with enterprise-grade support processes. | Support is split across two telco partner-support channels plus a separate card-acquirer support channel; no single point of operational accountability. |
| **Webhook/callback model (for T403)** | Webhook delivery with signature verification (HMAC-style secret) and a server-to-server verification/retrieve-transaction call as a reconciliation fallback. | Instant Payment Notification (IPN) callback plus a transaction status query endpoint used to authoritatively confirm status rather than trusting the callback alone. | Callback/redirect plus a token-based "verify token" status call used to authoritatively confirm status. | MTN and Airtel each have their own callback/webhook and status-query mechanisms, which would need two independent signature-verification and reconciliation implementations under T403 instead of one. |
| **Integration shape vs. this codebase's `IPaymentGateway`** | Maps cleanly: one `CreatePaymentAsync` call, one webhook handler, one `Supports(...)` check for currency/method. | Maps cleanly: one `CreatePaymentAsync` call (IFrame/hosted order creation), one IPN handler, one `Supports(...)` check. | Maps cleanly: one `CreatePaymentAsync` call (token creation + redirect), one callback handler, one `Supports(...)` check. | Does **not** map onto a single adapter cleanly — `PaymentService`/`IPaymentGateway` would need either two registered gateways selected by `MethodType`, or a composite gateway that internally branches by mobile-money network, plus a still-separate card gateway. More code and more operational surfaces for the same `MethodType.MobileMoney` value. |

## 3. Observations relevant to T401's decision

- All three aggregators (Flutterwave, Pesapal, DPO) can satisfy both required Phase 17 payment
  methods (`PaymentMethodType.Card`, `PaymentMethodType.MobileMoney`) through a **single**
  provider integration, which is the best fit for the existing `IPaymentGateway` abstraction: one
  `Supports(...)`/`CreatePaymentAsync(...)` implementation, one webhook handler (T403), one
  reconciliation/status-query mechanism, and one support relationship to operate.
- The direct-to-telco option removes aggregator markup on the mobile-money leg but reintroduces
  it on the card leg anyway (a card acquirer is still required), while roughly doubling the
  integration, webhook, and reconciliation surface (T402/T403) for no corresponding reduction in
  overall payment-method coverage. It is a plausible *future* optimization once transaction volume
  justifies the added engineering and support overhead, not a strong first production choice.
- Pesapal and DPO both have the longest standing operational history specifically in East
  Africa/Uganda; Flutterwave has the broadest pan-African reach and the most modern public
  developer experience. None of the three can be ruled out on pure technical/coverage grounds from
  this comparison alone.
- Published fee schedules for all three aggregators are generally negotiated per merchant and not
  reliably available without a direct commercial conversation; T401 must confirm current Uganda
  rate cards, settlement account requirements (local bank account, KYC/business registration
  documents), and minimum volume commitments directly with each provider's sales/partnerships team
  before making a final selection, rather than relying on this document's fee commentary alone.
- Whichever provider is selected, the integration should continue to use the existing
  `IPaymentGateway` contract (`PaymentGatewayCreateRequest`/`PaymentGatewayCreateResult`) so
  `PaymentService`, the `Payment` model, and the booking/pricing flow remain unchanged — only a
  new concrete gateway implementation and its options class should be added under
  `Infrastructure/Payments/`, per T402.

## 4. What this document is not

This is a technical/operational comparison to inform T401, not a legal, commercial, or compliance
review. It does not constitute a signed agreement, confirmed pricing, or a compliance
determination (e.g. PCI-DSS scope, data residency, or Bank of Uganda payment-system licensing
requirements for any given integration shape). T401 should involve whoever owns commercial and
compliance sign-off before a provider is contracted.
