# Uinsure Policy API

A REST API for selling and retrieving home insurance policies. It's built with C# on **.NET 10 isolated-process Azure Functions**.

This first iteration delivers the MoSCoW **Must** requirements:

| Requirement | Endpoint |
|---|---|
| Sell a policy | `POST /api/policies` |
| Retrieve a policy | `GET /api/policies/{reference}` |

Cancel and Renew (Should) and the Could requirements are planned next. The domain model already stores what they need: payment method, auto-renew, has-claims and refunds.

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
frontend/                              Placeholder for a future web client
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) **4.x, a recent build**. This is only needed to run the API, not the tests. Install it with `npm i -g azure-functions-core-tools@4`. Older builds, such as the one the winget package currently installs (4.0.5382), fail at startup with `Configuration is missing the 'HostEndpoint'`.

No database server or storage emulator is needed. Policies are stored in a local SQLite file (`policies.db`), which is created on first run.

### Run the tests

```bash
cd backend
dotnet test
```

With coverage (excludes Functions source-generated code):

```bash
dotnet test --settings coverage.runsettings
```

### Run the API

```bash
cd backend/src/Uinsure.PolicyApi.Functions
func start
```

The API listens on `http://localhost:7071/api`. Use [`backend/requests.http`](backend/requests.http) to send example requests. Adjust `startDate` so it falls within 60 days of today.

## API

### Sell a policy: `POST /api/policies`

```json
{
  "reference": "POL-0001",
  "insuranceType": "Household",
  "startDate": "2026-11-20",
  "amount": 325.50,
  "autoRenew": true,
  "paymentType": "Card",
  "property": {
    "addressLine1": "1 High Street",
    "addressLine2": "Little Town",
    "addressLine3": null,
    "postcode": "AB1 2CD"
  },
  "policyholders": [
    { "firstName": "Jane", "lastName": "Smith", "dateOfBirth": "1985-04-12" }
  ]
}
```

- `insuranceType` is `Household` or `BuyToLet`.
- `paymentType` is `Card`, `DirectDebit` or `Cheque`.

| Status | When |
|---|---|
| `201 Created` | Policy sold. The body is the policy, including its generated payment, and the `Location` header points to it. |
| `400 Bad Request` | A business rule failed. The response is a `ValidationProblemDetails` with the errors keyed by field, e.g. `StartDate`, `Property.Postcode`, `Policyholders[0]`. |
| `400 Bad Request` | The body isn't valid JSON, or a field has the wrong type or format. The `ProblemDetails` names the offending JSON path. |
| `409 Conflict` | A policy with this reference already exists. |
| `415 Unsupported Media Type` | The body isn't sent as `application/json`. |

### Retrieve a policy: `GET /api/policies/{reference}`

| Status | When |
|---|---|
| `200 OK` | The policy, with its property, policyholders, payments and refunds. |
| `404 Not Found` | No policy with that reference (`ProblemDetails`). |

All errors use [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`).

## Business rules implemented

When selling a policy:

- The policy reference must be unique.
- A policy can only be sold up to 60 days in advance.
- A policy is always 1 year in length. The end date is calculated, not supplied.
- A policy has between 1 and 3 policyholders.
- All policyholders must be at least 16 years old on the policy start date.
- Property address line 1 is required. The postcode is required, with a max length of 8 characters.
- A payment is recorded for the full amount using the chosen payment method.

The rule constants live in one place, [`PolicyRules`](backend/src/Uinsure.PolicyApi.Core/Domain/PolicyRules.cs), so validation, the domain model and the database schema agree.

## Assumptions

1. **60 days in advance.** The start date must be between today and today + 60 days, both inclusive. Start dates in the past are rejected.
2. **1 year in length.** End date = start date + 1 calendar year (e.g. 2026-11-20 → 2027-11-20). The server calculates it, so a consumer can't supply an inconsistent end date. A policy starting on 29 Feb ends on 28 Feb.
3. **"Over 16".** This is interpreted as aged 16 or older on the start date, so a policyholder whose 16th birthday is the start date is accepted. Someone born on 29 Feb turns a year older on 1 March in non-leap years (UK convention).
4. **References.** The consumer supplies the policy reference (max 20 characters). It is case-insensitive and stored in upper case, so `pol-1` and `POL-1` are the same policy. Payment references are generated by the server.
5. **Amount.** The amount is the annual premium, supplied by the consumer. It must be greater than 0 with at most 2 decimal places. Pricing is out of scope.
6. **Payment at sale.** Selling a policy takes one payment for the full amount, using the payment method in the request. Refunds (for Cancel) will go back to this method.
7. **Insurance type.** Household / Buy to Let is stored on the policy. No rules differ between the two types yet.
8. **Has claims.** A new policy always starts with `hasClaims: false`.
9. **Postcode.** It is trimmed and upper-cased, then validated for presence and length (≤ 8) only. There is no full UK postcode format check, to avoid rejecting valid edge cases.
10. **Field lengths.** These are not in the spec: names and address lines are limited to 100 characters each.
11. **"Today".** This is the current UTC date. Time is injected through `TimeProvider`, so date rules are fully testable.
12. **Auth.** Endpoints use `AuthorizationLevel.Anonymous` so reviewers can call them easily. A real deployment would use function keys, Entra ID or APIM.
13. **Persistence.** SQLite with `EnsureCreated()` is used instead of migrations, which is appropriate for a take-home exercise. Swapping to SQL Server, Cosmos DB or Table Storage only means implementing `IPolicyRepository`.
14. **Renewal window (for the Should phase).** "Can only be renewed 30 days before the end date" is read as any time within the 30 days up to the end date, not only on the 30th day.

## Design notes

- **Thin functions.** `PolicyFunctions` only parses HTTP and maps results to status codes. All business logic lives in `Core`, which has no dependency on Azure Functions or EF Core.
- **Results, not exceptions.** `PolicyService` returns a `ServiceResult` (`Success`, `ValidationFailed`, `Conflict` or `NotFound`), so expected outcomes are explicit and easy to test.
- **Explicit missing fields.** Request value types are nullable. A missing `startDate` or `paymentType` gives a clear "required" error instead of silently becoming `0001-01-01` or the first enum value.
- **Concurrency.** The policy reference is the primary key. If two concurrent sales of the same reference both pass the existence check, the database constraint still rejects the second, and it is returned as `409`.
- **Tests.** There are 108 tests: unit tests cover every rule boundary (day 60/61, 0/1/3/4 policyholders, the 16th birthday, leap days, postcode length), and integration tests drive the real function methods against in-memory SQLite. Line coverage is roughly 95%+ across the projects.
