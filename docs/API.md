# Policy API reference

The full request and response reference. The same information is in Swagger UI (`http://localhost:7071/api/swagger/ui`) while the API is running.

## Policy shape

A policy is made up of one-year **terms**: term 1 when it's sold, plus one for each renewal. The top-level `startDate`, `endDate` and `amount` describe the latest term. The `terms` array has the full history, and each payment and refund names the `termNumber` it belongs to. `status` is `Active` or `Cancelled`.

## Errors

All errors use [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) (`application/problem+json`). Invalid input returns `400`, and a request that conflicts with the policy's current state returns `409`.

## Sell a policy: `POST /api/policies`

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
- `paymentType` is `Card`, `DirectDebit` or `Cheque`. `Cheque` is only allowed when `autoRenew` is `false`.

| Status | When |
|---|---|
| `201 Created` | Policy sold. The body is the policy, including its generated payment, and the `Location` header points to it. |
| `400 Bad Request` | A business rule failed. The response is a `ValidationProblemDetails` with the errors keyed by field, e.g. `StartDate`, `Property.Postcode`, `Policyholders[0]`. |
| `400 Bad Request` | The body isn't valid JSON, or a field has the wrong type or format. The `ProblemDetails` names the offending JSON path. |
| `409 Conflict` | A policy with this reference already exists. |
| `415 Unsupported Media Type` | The body isn't sent as `application/json`. |

## Retrieve a policy: `GET /api/policies/{reference}`

| Status | When |
|---|---|
| `200 OK` | The policy, with its property, policyholders, terms, payments and refunds. |
| `404 Not Found` | No policy with that reference (`ProblemDetails`). |

## Cancel a policy: `POST /api/policies/{reference}/cancel`

```json
{ "cancellationDate": "2027-02-28" }
```

Cancels the policy from the given date (today or later, and no later than the end date). Each paid term is refunded to the same method as its payment:

- **Before the term starts:** a full refund.
- **Within the first 14 days of the term (cooling off):** a full refund.
- **After that:** pro rata, `amount × unused days ÷ days in the term`, rounded to the nearest penny.

| Status | When |
|---|---|
| `200 OK` | Cancelled. The body is the policy, with `status: "Cancelled"` and its `refunds`. |
| `400 Bad Request` | The date is missing, in the past or after the end date, or the body isn't valid JSON. |
| `404 Not Found` | No policy with that reference. |
| `409 Conflict` | The policy is already cancelled, or was changed by another request at the same time. |
| `415 Unsupported Media Type` | The body isn't sent as `application/json`. |

## Renew a policy: `POST /api/policies/{reference}/renew`

```json
{ "amount": 340.00, "paymentType": "Card" }
```

Adds a new one-year term starting on the current end date, for the given premium. It's allowed from 30 days before the end date up to and including the end date. If the policy is set to auto renew, a payment is taken for the new term, so `paymentType` is required. Otherwise no payment is created and `paymentType` can be left out.

| Status | When |
|---|---|
| `200 OK` | Renewed. The body is the policy with the new term, plus its payment if one was taken. |
| `400 Bad Request` | The amount is invalid, `paymentType` is missing or `Cheque` for an auto-renewing policy, or the body isn't valid JSON. |
| `404 Not Found` | No policy with that reference. |
| `409 Conflict` | Outside the renewal window (the message says when it opens), already renewed, cancelled, or changed by another request. |
| `415 Unsupported Media Type` | The body isn't sent as `application/json`. |

## Quote the cost of cancelling: `GET /api/policies/{reference}/cancellation-quote?cancellationDate=2027-02-28`

Returns the refunds cancelling on that date would give, using exactly the same calculation as cancelling, without changing the policy. `cancellationDate` is optional and defaults to today; it follows the same rules as cancel.

```json
{
  "reference": "POL-0001",
  "cancellationDate": "2027-02-28",
  "hasClaims": false,
  "totalRefund": 265.00,
  "refunds": [ { "termNumber": 1, "type": "Card", "amount": 265.00 } ]
}
```

| Status | When |
|---|---|
| `200 OK` | The quote. `refunds` is empty (and `totalRefund` is 0) if the policy has claims or nothing is refundable. |
| `400 Bad Request` | The date isn't `yyyy-MM-dd`, is in the past, or is after the end date. |
| `404 Not Found` | No policy with that reference. |
| `409 Conflict` | The policy is already cancelled. |

## Record a claim: `POST /api/policies/{reference}/claims`

No body. Marks the policy as having claims (`hasClaims: true`). From then on, cancelling it gives no refund.

| Status | When |
|---|---|
| `200 OK` | Claim recorded. The body is the policy. Recording another claim changes nothing. |
| `404 Not Found` | No policy with that reference. |
| `409 Conflict` | The policy is cancelled, or was changed by another request at the same time. |

## Swagger notes

Swagger is generated by Microsoft's [Azure Functions OpenAPI extension](https://github.com/Azure/azure-functions-openapi-extension) from `[OpenApi…]` attributes on each function. The request example uses a start date two weeks from today, so **Try it out** works without editing.

The extension predates .NET's System.Text.Json and `DateOnly`, so two adjustments make the docs match the real API:

- `InsuranceType`, `PaymentType` and `PolicyStatus` also carry Newtonsoft's `StringEnumConverter` attribute. The extension reads it to document the enums by name; the API itself still serialises with System.Text.Json.
- `DateOnlySchemaFilter` documents `DateOnly` as an ISO date string (`2026-11-20`) instead of a `{year, month, day…}` object.
