# Uinsure Policy API

A REST API for selling, retrieving, cancelling and renewing home insurance policies, built with C# on **.NET 10 isolated-process Azure Functions**.

## Quick start

```bash
cd backend && dotnet test                                 # no setup needed
cd src/Uinsure.PolicyApi.Functions && dotnet run          # needs Azure Functions Core Tools 4.16+
```

Then open Swagger UI at http://localhost:7071/api/swagger/ui.

## What's delivered

All of the MoSCoW requirements: **Must**, **Should** and **Could**.

| Priority | Requirement | Endpoint |
|---|---|---|
| Must | Sell a policy | `POST /api/policies` |
| Must | Retrieve a policy | `GET /api/policies/{reference}` |
| Should | Cancel a policy | `POST /api/policies/{reference}/cancel` |
| Should | Renew a policy | `POST /api/policies/{reference}/renew` |
| Could | Quote the cost of cancelling | `GET /api/policies/{reference}/cancellation-quote` |
| Could | No refund for a policy with claims | `POST /api/policies/{reference}/claims` records a claim |
| Could | No auto renewal paid by cheque | Enforced on sell and renew |

Request and response details, status codes and examples are in [docs/API.md](docs/API.md) and in Swagger.

## Approach

I treated the MoSCoW list as a delivery order. I built the Musts end to end first (domain, validation, persistence, HTTP, tests), then the Shoulds, then the Coulds, so there was a working, tested API at every stage that could have shipped.

The infrastructure is deliberately simple (SQLite, anonymous endpoints) so a reviewer can clone and run it with no database or emulator to set up.

## Repository layout

```
backend/
  src/
    Uinsure.PolicyApi.Core/            Domain model, business rules, validation, application service
    Uinsure.PolicyApi.Infrastructure/  EF Core + SQLite persistence
    Uinsure.PolicyApi.Functions/       Azure Functions HTTP layer (thin: parse → service → map to HTTP)
  tests/
    Uinsure.PolicyApi.Core.Tests/      Unit tests: validation rules, domain, service
    Uinsure.PolicyApi.Functions.Tests/ Integration tests: functions + real services + in-memory SQLite
  requests.http                        Sample requests for manual testing
docs/
  API.md                               Full API reference
```

## Running it

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) **4.16 or later**, to run the API (not needed for the tests). Install it with `npm i -g azure-functions-core-tools@4` and check with `func --version`. Older builds, such as the winget package, fail at startup.

No database server or storage emulator is needed. Policies are stored in a local SQLite file (`policies.db`), created on first run.

### Tests

```bash
cd backend
dotnet test
dotnet test --settings coverage.runsettings   # with coverage, excluding source-generated code
```

### API

```bash
cd backend/src/Uinsure.PolicyApi.Functions
dotnet run
```

The API listens on `http://localhost:7071/api`.

- **Swagger UI:** http://localhost:7071/api/swagger/ui. The sell example uses a start date two weeks from today, so **Try it out** works without editing.
- **Sample requests:** [`backend/requests.http`](backend/requests.http) has a scripted walkthrough (sell → get → quote → cancel, plus the cheque and claim cases). Open it in Visual Studio, Rider, or VS Code with the REST Client extension, and send each request (**Send Request**) from top to bottom. Adjust `startDate` so it falls within 60 days of today.

### Trying date-dependent rules (`TimeOverride__Today`)

A new policy can't be renewed until 30 days before it ends, which is at least ten months away. To try renewal (or late cancellations) by hand, set `TimeOverride__Today` in `local.settings.json` to a `yyyy-MM-dd` date and restart the API. For example, sell a policy starting `2026-11-20`, then restart with `"TimeOverride__Today": "2027-11-05"` and renew it. The API prints a warning at startup while the override is active. It is a local demo aid only; leave it empty for normal use.

## Business rules implemented

When selling a policy:

- The policy reference must be unique.
- A policy can only be sold up to 60 days in advance.
- A policy is always 1 year in length. The end date is calculated, not supplied.
- A policy has between 1 and 3 policyholders.
- All policyholders must be at least 16 years old on the policy start date.
- Property address line 1 is required. The postcode is required, with a max length of 8 characters.
- A payment is recorded for the full amount using the chosen payment method.
- An auto-renewing policy can't be paid by cheque.

When renewing a policy:

- A policy can only be renewed in the 30 days before its end date, and not after the end date.
- An auto-renewing policy has a payment created; a policy that doesn't auto renew has no payment created.
- The renewal payment for an auto-renewing policy can't be by cheque.

When cancelling a policy:

- Cancellation before the start date results in a full refund.
- Cancellation on or after the start date and within the 14-day cooling-off period results in a full refund.
- Cancellation after the cooling-off period results in a pro rata refund for the days of insurance not used.
- The refund is made to the same method as the original payment.
- A policy with claims gets no refund.
- The cost of cancelling on a given date can be quoted without cancelling.

The rule constants live in one place, [`PolicyRules`](backend/src/Uinsure.PolicyApi.Core/Domain/PolicyRules.cs), so validation, the domain model and the database schema agree.

## Assumptions and interpretations of the brief

1. **References.** The consumer supplies the policy reference (max 20 characters). It is case-insensitive and stored in upper case, so `pol-1` and `POL-1` are the same policy. Payment references are generated by the server.
2. **Amount.** The amount is the annual premium, supplied by the consumer. It must be greater than 0 with at most 2 decimal places. Pricing is out of scope.
3. **Auth.** Endpoints use `AuthorizationLevel.Anonymous` so reviewers can call them easily. A real deployment would use function keys, Entra ID or APIM.
4. **Renewal window.** "Can only be renewed 30 days before the end date" is read as any time from 30 days before the end date up to and including the end date, not only on the 30th day. "Today" is the current UTC date.
5. **A policy can only be renewed once per term.** Renewing again before the new term starts returns `409`. It can be renewed again in the next term's window.
6. **Cancelled policies.** These can't be cancelled again or renewed (`409`).
7. **Recording claims.** Claims handling is out of scope, so the API only records *that* a policy has a claim: no claim details are stored, and it can't be undone. A further claim changes nothing. A claim can't be recorded on a cancelled policy (`409`).
8. **No refund with claims.** "No refund if there are claims" applies to the whole policy: every term, including a renewed term that hasn't started, gets no refund. The policy is still cancelled.

## Design decisions and trade-offs

- **Business rules in a framework-free domain.** `Policy.Cancel`, `Policy.Renew` and `Policy.RecordClaim` enforce the state rules, and `RefundCalculator` is a pure function tested rule by rule. `Core` has no dependency on Azure Functions or EF Core, so the rules can be tested in milliseconds and the hosting can change without touching them.
- **Thin functions, one per file.** Each function only parses HTTP, calls the service and maps the result to a status code, using shared helpers in `Http/HttpResults.cs`.
- **One refund calculation.** `Policy.Cancel` issues exactly the refunds `Policy.QuoteCancellation` calculates, so a quote can never disagree with the real cancellation.
- **A policy is a series of one-year terms.** Rather than a single start and end date, each renewal adds a term with its own payment. This keeps the renewal history and lets a renewed-but-not-started term be refunded separately.
- **Client-supplied reference as the primary key.** It matches the brief and makes selling safe to retry: a repeated request gets `409` instead of creating a duplicate policy. *Cost:* references could collide between consumers and may be guessable, which auth mitigates (see [What I'd do next](#what-id-do-next)).
- **Optimistic concurrency.** Cancel, renew and claim check a `Version` column on save, so two simultaneous cancellations can't both create refunds; the second gets `409` and can retry. *Cost:* the caller has to handle a retry.
- **Explicit missing fields.** Request value types are nullable, so a missing `startDate` or `paymentType` gives a clear "required" error instead of silently becoming `0001-01-01` or the first enum value.
- **Injected clock.** All "today" logic goes through `TimeProvider`, so tests use a fake clock and the date rules are tested on exact boundaries.
- **SQLite.** Zero setup for demo purposes. *Cost:* a single local file can't be shared by multiple instances, and there are no migrations.

### Testing

The tests are in three layers:

- **Unit tests** cover every rule boundary: day 60/61, 0/1/3/4 policyholders, the 16th birthday, leap days, postcode length, cooling-off day 13/14, renewal window day 30/31, pro rata rounding, and cheque with and without auto renew.
- **Integration tests** drive the real function methods against in-memory SQLite, with a fake clock for the date rules.
- **OpenAPI tests** check that every function and status code is documented.

Tests are named `GIVEN_..._WHEN_..._THEN_...` and split into Arrange / Act / Assert sections. The fakes are hand-written rather than generated by a mocking library. Line coverage is about 90% for the Functions project and 98–99% for Core and Infrastructure.

## What I'd do next

1. **Authentication and authorisation.** The endpoints are anonymous so reviewers can call them. First step: Entra ID (or APIM in front) with scopes per operation, so a consumer can only see and change its own policies.
2. **A production database.** Move from SQLite to Azure SQL (EF Core makes this mostly a provider and connection-string change).
3. **Observability.** Application Insights with structured logging, correlation IDs and alerts on error rates. Today there is only `ILogger` output.
4. **Audit trail and claims.** Record who changed what and when, and turn the `HasClaims` flag into a proper claims model (or an integration with the claims system).

Deliberately left out of scope: pricing, a list/search endpoint, editing policy details after sale, and a front end.
