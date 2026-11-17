---
name: angular-standards
description: Client-side conventions for the Combination Generator (Angular 20, Signals). Load before creating or editing any file under client/.
---

# Angular 20 Standards

## Architecture
```
src/app/
  core/        singletons: API services, interceptors, models (no UI)
  state/       signal stores (single source of truth)
  features/    feature folders; one smart "page" component each
  shared/      reusable dumb components, pipes, directives
```
- **Smart / dumb split**: only the feature's page component injects the store. Child components are presentational — they receive data via `input()` and emit via `output()`; they never inject services.
- **Store** (`state/permutation.store.ts`): an `@Injectable({ providedIn: 'root' })` class holding private `signal()`s, exposing them read-only (`.asReadonly()`) plus `computed()` derived values, and public action methods (`start(n)`, `next()`, `loadPage(page)`, `back()`, `reset()`). The store calls the API service; components call the store.
- **API service** (`core/api`): typed wrapper over `HttpClient`, returns `Observable<Dto>`; no state.
- **Session header**: added by a functional `HttpInterceptorFn` reading the sessionId from the store/session holder.

## Component rules
- Standalone components only (default in v20; don't set `standalone: true` explicitly).
- `changeDetection: ChangeDetectionStrategy.OnPush` on every component.
- Use `input()`, `input.required()`, `output()`, `model()` — not `@Input/@Output` decorators.
- Use `inject()` — not constructor injection.
- Templates use the built-in control flow: `@if`, `@for (… ; track …)`, `@switch`. No `*ngIf/*ngFor`.
- No manual `.subscribe()` in components. Inside the store, convert with `firstValueFrom` or `rxMethod`-style helpers, or subscribe once and write into signals; always handle errors.
- Keep components small: if a template exceeds ~60 lines or has two responsibilities, split it.
- File naming: `kebab-case.component.ts` with inline template/styles allowed for tiny dumb components; otherwise separate `.html` / `.scss`.

## Big numbers
- Counts and indices arrive from the server as **strings** (they can exceed `Number.MAX_SAFE_INTEGER`). Convert with `BigInt(value)` for arithmetic and format with the shared `thousands` pipe. Never `Number()` / `parseInt` them.

## Error handling & UX
- Global error surface in the store (`error` signal) shown by a small dumb component.
- Session-not-found (404) → reset to the input screen with a message.
- Disable buttons while a request is in flight (`loading` signal).
- Basic styling is sufficient; keep it clean and readable.

## Testing
- Every component, pipe, store and service gets a `.spec.ts`.
- Dumb components: test inputs render and outputs emit.
- Store: test with `HttpTestingController` (`provideHttpClient()`, `provideHttpClientTesting()`).

## Commands
```
cd client
npm ci
npx ng serve          # http://localhost:4200, proxy to the API via proxy.conf.json
npx ng build
npx ng test --watch=false
```
Before finishing any task: `ng build` with no warnings and all tests green.
