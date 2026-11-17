# Combination Generator – Client (Angular 20)

## Commands

```bash
npm ci
npx ng serve                          # http://localhost:4200, /api proxied to http://localhost:5002 (proxy.conf.json)
npx ng serve --configuration mock     # same UI against the in-memory mock API (no server needed)
npx ng build
npx ng test --watch=false             # Karma, ChromeHeadless
```

## Structure

```
src/app/
  core/models        DTOs matching docs/API_CONTRACT.md (big numbers are strings)
  core/api           PERMUTATION_API token, HTTP service, in-memory mock
  core/session       SessionIdHolder (read by the interceptor)
  core/interceptors  sessionInterceptor (adds X-Session-Id)
  state              PermutationStore (signals, single source of truth)
  features/generator generator-page (smart) + presentational components
  shared             paginator, error-message, thousands/permutation pipes
```
