# Combination Generator – API Contract

Version: 1.1 · Base path: `/api/permutations` · Format: JSON (`application/json`, camelCase)

The server generates the permutations of `1..n` in **lexicographic order**, one step at a time, keeping the
state of each user's session on the server. Nothing is precomputed: every call computes only what it returns.

## Conventions

| Topic | Rule |
|---|---|
| Indices | All indices are **1-based display numbers** (the first permutation is `#1`). |
| Large numbers | `totalCount`, `index`, `page` (in responses), `totalItems`, `totalPages`, `firstIndex` are JSON **strings** of decimal digits, because `20! = 2432902008176640000` exceeds `2^53` (the largest integer a JavaScript `number` represents exactly). `n`, `pageSize` and the permutation elements are JSON numbers. The `page` query parameter is plain text in the URL and is parsed as a 64-bit integer, so any page up to `totalPages` is addressable (for n = 20 and pageSize 1 that is about 2.4·10^18, beyond `2^53`). |
| Session header | Every call except `start` must send `X-Session-Id: <guid>` (the value returned by `start`). |
| Errors | Always `application/problem+json` (RFC 7807 ProblemDetails), see [Errors](#errors). Never a plain string. |
| Absent values | Fields that have no value are present and `null` (never omitted). |
| CORS | Only the Angular dev origin `http://localhost:4200` is allowed, and it must be allowed to send the `X-Session-Id` header. In development the Angular dev server also proxies `/api` to the API, so the browser normally makes same-origin calls. |
| Session not found | Unknown and expired sessions both return `404`; for the client the reaction is the same (return to the input screen). |

### Reference data for n = 3

All examples below use `n = 3`. The complete lexicographic sequence is:

| Index | Permutation |
|---|---|
| 1 | `[1,2,3]` |
| 2 | `[1,3,2]` |
| 3 | `[2,1,3]` |
| 4 | `[2,3,1]` |
| 5 | `[3,1,2]` |
| 6 | `[3,2,1]` |

---

## Endpoints

### 1. `POST /api/permutations/start`

Creates a **new** session for the given `n`. Nothing is displayed yet (current position = "before #1").
Calling `start` again – with the same or a different `n` – creates another, independent session; this is how
the client implements **Reset**. The previous session is simply abandoned and expires on its own.

**Request**

```http
POST /api/permutations/start
Content-Type: application/json

{ "n": 3 }
```

**Response `200 OK`**

```json
{
  "sessionId": "8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01",
  "n": 3,
  "totalCount": "6"
}
```

| Field | Type | Description |
|---|---|---|
| `sessionId` | string (GUID) | Identifier to send in `X-Session-Id` on every subsequent call. |
| `n` | number | Echo of the requested `n`. |
| `totalCount` | string | `n!` – total number of permutations. |

**Errors:** `400` if the body is missing/malformed or `n` is not an integer in `1..20` (always
`errors: { "n": ["n must be between 1 and 20."] }`, see [Errors](#errors)); `503` when the server's session limit is
reached (see [Session](#session)).

---

### 2. `GET /api/permutations/next`

Advances the session by one and returns the permutation that now becomes the current one.
The first call after `start` returns `#1`. Calling `next` also **ends "All" mode** (see [Paging anchor](#paging-anchor)).

**Request**

```http
GET /api/permutations/next
X-Session-Id: 8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01
```

**Response `200 OK` – a next permutation exists** (first call after `start`)

```json
{
  "index": "1",
  "permutation": [1, 2, 3],
  "hasMore": true,
  "message": null
}
```

**Response `200 OK` – the last permutation** (sixth call)

```json
{
  "index": "6",
  "permutation": [3, 2, 1],
  "hasMore": false,
  "message": null
}
```

**Response `200 OK` – no next permutation** (any call after `#6` has been reached)

```json
{
  "index": null,
  "permutation": null,
  "hasMore": false,
  "message": "No more combinations"
}
```

| Field | Type | Description |
|---|---|---|
| `index` | string \| null | 1-based index of the returned permutation; `null` when none is returned. |
| `permutation` | number[] \| null | The permutation (length `n`); `null` when none is returned. |
| `hasMore` | boolean | `true` if **another** call to `next` would return a permutation. |
| `message` | string \| null | `"No more combinations"` when nothing is returned, otherwise `null`. |

Running out of permutations is a normal outcome, not an error: it is `200`, and the session position is unchanged
(it stays on the last permutation).

**Errors:** `400` missing/invalid `X-Session-Id`; `404` unknown or expired session.

---

### 3. `GET /api/permutations/all?pageSize={pageSize}&page={page}`

Returns one page of the permutations that follow the **anchor** – the permutation that was displayed when the
user entered "All" mode (see [Paging anchor](#paging-anchor)).

**Query parameters**

| Name | Type | Default | Rule |
|---|---|---|---|
| `pageSize` | integer | `50` | `1..1000` |
| `page` | integer (64-bit, decimal text) | `1` | `>= 1` and `<= totalPages` (page `1` is always accepted, even when `totalPages` is `"0"`) |

**Request** (the user is viewing `#2` and enters "All" mode with a page size of 2)

```http
GET /api/permutations/all?pageSize=2&page=1
X-Session-Id: 8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01
```

**Response `200 OK`**

```json
{
  "items": [
    { "index": "3", "permutation": [2, 1, 3] },
    { "index": "4", "permutation": [2, 3, 1] }
  ],
  "page": "1",
  "pageSize": 2,
  "totalPages": "2",
  "totalItems": "4",
  "firstIndex": "3",
  "message": null
}
```

**Next page** – `GET /api/permutations/all?pageSize=2&page=2` (same anchor):

```json
{
  "items": [
    { "index": "5", "permutation": [3, 1, 2] },
    { "index": "6", "permutation": [3, 2, 1] }
  ],
  "page": "2",
  "pageSize": 2,
  "totalPages": "2",
  "totalItems": "4",
  "firstIndex": "3",
  "message": null
}
```

**Response `200 OK` – nothing remains after the anchor** (the user is viewing `#6`)

```json
{
  "items": [],
  "page": "1",
  "pageSize": 2,
  "totalPages": "0",
  "totalItems": "0",
  "firstIndex": null,
  "message": "No more combinations"
}
```

| Field | Type | Description |
|---|---|---|
| `items` | array | Permutations of this page, in order; each `{ index: string, permutation: number[] }`. |
| `page` | string | Echo of the requested page (string, like `totalPages`). |
| `pageSize` | number | Echo of the effective page size. |
| `totalPages` | string | `ceil(totalItems / pageSize)`. |
| `totalItems` | string | Number of permutations after the anchor: `n! − anchorIndex`. |
| `firstIndex` | string \| null | Index of the first permutation after the anchor (`anchorIndex + 1`); `null` when `totalItems` is `"0"`. |
| `message` | string \| null | `"No more combinations"` when `totalItems` is `"0"`, otherwise `null`. |

**Errors:** `400` invalid `pageSize`/`page` (including `page > totalPages` and values that are not integers),
missing/invalid `X-Session-Id`; `404` unknown or expired session.

---

### 4. `POST /api/permutations/back`

Leaves "All" mode (the user pressed **Back**). Clears the session's paging anchor **without moving** the current
position: that position already is the last item of the last page served, which is exactly what the client shows
after Back (it has the item already, so the response has no body). The next `/all` therefore captures a new anchor
right after that item, and `/next` continues from it. Idempotent: calling it outside "All" mode changes nothing.

The client **must** call it when the user goes Back; otherwise the server stays in "All" mode and a later "All"
would reuse the stale anchor of the previous visit (see the [example](#back-returning-from-all-mode)).

**Request** (no body)

```http
POST /api/permutations/back
X-Session-Id: 8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01
```

**Response `204 No Content`** – empty body.

**Errors:** `400` missing/invalid `X-Session-Id`; `404` unknown or expired session.

---

## Semantics

### Session

- A session is created by `POST /start` and identified by its `sessionId` (GUID).
- The server keeps, per session: `n`, the **current position** (index of the currently displayed permutation;
  `0` = nothing displayed yet), and the **"All" mode anchor** (see below).
- Sliding expiration of **30 minutes**: every call that reaches an existing session resets its timer – including a
  call that is then rejected because of the session's state (`page > totalPages`). Calls rejected before the session
  is looked up (missing/invalid `X-Session-Id`, invalid `pageSize`, `page < 1`, non-integer query values) do not
  touch it. After expiry the session is gone and every call returns `404`.
- Sessions are fully isolated from each other; concurrent requests on the same session are serialized on the server.
- The server holds at most **`Sessions:MaxSessions`** live sessions (configuration, default `10000`). When the
  limit is reached, `start` returns `503` ProblemDetails (see [Errors](#errors)); live sessions are **never** evicted
  to make room, and existing sessions keep working. Room frees up as idle sessions expire (expired sessions are
  purged before `start` is rejected).

### Paging anchor

- Entering "All" mode shows the permutations that come **right after the currently displayed one**:
  if `#1000` is displayed, page 1 starts at `#1001`. If nothing has been displayed yet (right after `start`),
  page 1 starts at `#1`.
- The anchor is **server-side state** – there is no anchor query parameter. The server captures it on the
  **first `/all` request after a `/start`, `/next` or `/back`**, as the then-current position.
- While the user pages (any number of `/all` calls, any `page`/`pageSize`), the anchor stays **fixed**, so
  `firstIndex`, `totalItems` and page boundaries are stable.
- Only `/next` and `/back` **end "All" mode** (clear the anchor); the following `/all` captures a new anchor.
  `/start` creates a new session, which has no anchor.

Example (n = 3, page size 2): `next` → `#1`, `next` → `#2`, `all?page=1` → anchor `#2`, items `#3,#4`;
`all?page=2` → still anchor `#2`, items `#5,#6`.

### Back (returning from "All" mode)

- Every `/all` response that returns at least one item moves the session's **current position to the last item
  of the returned page**. An empty response leaves the position unchanged.
- When the user goes Back to single mode, the client calls `POST /back` (ends "All" mode on the server) and
  displays that last item itself (it already has it – `/back` returns no body). The following `/next` continues
  right after it, and the following `/all` starts right after it.

Example (continuing above): after `all?page=1` (items `#3,#4`) the current position is `#4`; Back shows `#4`;
`next` returns `#5`. Had the user stopped on page 2 (items `#5,#6`), Back would show `#6` and `next` would return
`"No more combinations"`.

Example – Back, then "All" again (n = 3, page size 2):

| Step | Call | Result | Anchor after | Position after |
|---|---|---|---|---|
| 1 | `next` | `#1` | – | `#1` |
| 2 | `all?pageSize=2&page=1` | `#2,#3` (`firstIndex "2"`, `totalItems "5"`) | `#1` | `#3` |
| 3 | `all?pageSize=2&page=2` | `#4,#5` | `#1` | `#5` |
| 4 | `back` | `204`; the client shows `#5` | – | `#5` |
| 5 | `all?pageSize=2&page=1` | `#6` (`firstIndex "6"`, `totalItems "1"`) | `#5` | `#6` |

Without step 4 the server would still be in "All" mode and step 5 would return `#2,#3` again (stale anchor).

### No precomputation

Each call computes only what it returns: `next` derives one successor permutation; `all` computes the first
permutation of the page directly from its index (unranking) and then derives the rest of the page step by step.
Memory and time per call are `O(n)` and `O(pageSize · n)` respectively, independent of `n!`.

---

## Errors

All errors are RFC 7807 ProblemDetails, `Content-Type: application/problem+json`.
Validation errors (`400`) additionally carry an `errors` object mapping the offending field to its messages.
A `traceId` extension is included for correlation with server logs.

### `400` – invalid `n`

`POST /api/permutations/start` with `{ "n": 21 }`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "n must be between 1 and 20.",
  "errors": { "n": ["n must be between 1 and 20."] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

The **same response** is returned when `n` is missing or the body is malformed: no body, `{}`, `{ "n": null }`,
`{ "n": "abc" }`, `{ "n": 3.5 }`, `{ "n": 99999999999 }`, invalid JSON, or a JSON value that is not an object.
The `errors` key is always `n`. (A non-empty body sent with a non-JSON `Content-Type` is rejected by the framework
with `415` ProblemDetails.)

### `400` – invalid `pageSize`

`GET /api/permutations/all?pageSize=0`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "pageSize must be between 1 and 1000.",
  "errors": { "pageSize": ["pageSize must be between 1 and 1000."] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

A `pageSize` that is not a 32-bit integer (`abc`, `1.5`, `99999999999`) yields the same response.

### `400` – invalid `page`

`GET /api/permutations/all?pageSize=2&page=3` (only 2 pages exist)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "page must be between 1 and 2.",
  "errors": { "page": ["page must be between 1 and 2."] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

`page=0` (or negative) yields the same shape with `"page must be greater than or equal to 1."`.
A `page` that is not a 64-bit integer (`abc`, `1.5`, `1e3`, `9223372036854775808`) yields the same shape with
`"page must be an integer greater than or equal to 1."` (the upper bound is not known before the session is read).

If several query values are invalid at once, `errors` lists each offending field and `detail` repeats the first
message.

### `400` – missing or invalid session header

`GET /api/permutations/next` without `X-Session-Id`, or with a value that is not a GUID

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The X-Session-Id header is required and must be a GUID.",
  "errors": { "X-Session-Id": ["The X-Session-Id header is required and must be a GUID."] },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

### `404` – unknown or expired session

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Session not found",
  "status": 404,
  "detail": "Session '8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01' was not found or has expired. Start a new session.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

### `503` – session limit reached

`POST /api/permutations/start` while the server already holds `Sessions:MaxSessions` live sessions

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Session limit reached",
  "status": 503,
  "detail": "The server has reached its maximum number of active sessions. Try again later.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

### Status code summary

| Endpoint | Success | 400 | 404 | 503 |
|---|---|---|---|---|
| `POST /start` | `200` session created | invalid/missing `n`, malformed body | – | session limit reached |
| `GET /next` | `200` permutation or "No more combinations" | missing/invalid `X-Session-Id` | unknown/expired session | – |
| `GET /all` | `200` page (possibly empty with "No more combinations") | invalid `pageSize`/`page`, missing/invalid `X-Session-Id` | unknown/expired session | – |
| `POST /back` | `204` "All" mode ended | missing/invalid `X-Session-Id` | unknown/expired session | – |

Unexpected server failures return `500` ProblemDetails without internal details.
