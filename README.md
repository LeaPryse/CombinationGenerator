# Combination Generator

A client/server web app that generates all permutations of `[1..n]` (`1 ≤ n ≤ 20`) in lexicographic order. The user enters `n`, sees the total count (`n!`), and then either steps through the permutations one by one ("Next") or browses them in pages ("All"). Nothing is precomputed: `20!` is about 2.4·10^18, so permutations are generated on demand.

- **Server:** .NET 8, Clean Architecture (Api / Application / Domain / Infrastructure), in-memory session store.
- **Client:** Angular 20, signal store, paginator for very large page counts.

## How it works

- `POST /api/permutations/start` creates a session and returns its id; the client sends it in the `X-Session-Id` header on every later call.
- `GET /next` returns the next permutation, `GET /all?page=&pageSize=` returns a page, `POST /back` leaves "All" mode.
- The next permutation is computed with `NextPermutation`; the first item of a page is computed directly from its index (`Unrank`), so any page is reachable in O(n²) regardless of `n!`.
- Counts and indices are sent as JSON strings, since `20!` exceeds `2^53`.

Details: [docs/API_CONTRACT.md](docs/API_CONTRACT.md). Answers to the assignment questions: [docs/ANSWERS.md](docs/ANSWERS.md).

## Run

Requirements: .NET 8 SDK, Node.js with npm.

```bash
# Server (http://localhost:5002)
cd server/src/CombinationGenerator.Api
dotnet run --launch-profile http

# Client (http://localhost:4200, proxies /api to the server)
cd client
npm install
npm start
```

Swagger UI is available in Development at `/swagger`.

## Test

```bash
cd server && dotnet test     # Domain, Application, Infrastructure and API integration tests
cd client && npm test        # Angular unit tests
```

## Structure

```
server/src/   Api, Application, Domain, Infrastructure
server/tests/ Domain, Application, Infrastructure and API integration tests
client/       Angular app
docs/         API contract and assignment answers
```
