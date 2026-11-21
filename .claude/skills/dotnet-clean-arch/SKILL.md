---
name: dotnet-clean-arch
description: Server-side conventions for the Combination Generator (.NET 8, Clean Architecture). Load before creating or editing any file under server/.
---

# .NET 8 Clean Architecture Standards

## Layers and dependency direction
```
Api ──► Application ──► Domain
 │            ▲
 └──► Infrastructure ─┘
```
| Project | Contains | May reference |
|---|---|---|
| `CombinationGenerator.Domain` | Pure algorithms, entities, domain exceptions. No I/O, no DI, no NuGet packages. | nothing |
| `CombinationGenerator.Application` | Use-case services, DTOs, validation, abstractions (`ISessionStore`). | Domain |
| `CombinationGenerator.Infrastructure` | Implementations of Application abstractions (`InMemorySessionStore`). | Application, Domain |
| `CombinationGenerator.Api` | Controllers, middleware, DI composition root, CORS, Swagger. | Application, Infrastructure |

**Never** reference Infrastructure from Application, or Api types from anywhere else.

### Mapping to classic N-tier terms
Use this mapping when explaining the architecture (README, INTERVIEW.md, code comments):

| Classic N-tier | Here | Why it differs |
|---|---|---|
| Controller (API) | `Api` | Same role: HTTP in/out only. |
| Model / DTO | `Application/Dtos` | DTOs live next to the use cases that produce them. |
| BL (business logic) | `Domain` **+** `Application` | BL is split: **Domain** = the pure calculation (permutation algorithms, session entity) with zero dependencies; **Application** = orchestration services (load session → calculate → save → map to DTO). |
| DAL (data access) | `Infrastructure` | Implements `ISessionStore` (in-memory today, Redis tomorrow). BL depends on the interface, not on the DAL (Dependency Inversion). |

## Project settings (via `server/Directory.Build.props`)
- `TargetFramework` `net8.0`, `Nullable` enable, `ImplicitUsings` enable
- `TreatWarningsAsErrors` true, `LangVersion` latest
- File-scoped namespaces; namespace = folder path

## Coding rules
- **Controllers are thin**: validate route/query binding → call one service method → map to HTTP result. No business logic.
- **DTOs are `sealed record`s** in `Application/Dtos`. Never expose Domain types over HTTP.
- **Large numbers** (counts, indices that may exceed 2^53) are serialized as **strings** in DTOs.
- **Errors**: throw typed exceptions (`SessionNotFoundException`, `InvalidNException`, …) from Application/Domain; a single exception-handling middleware (`IExceptionHandler`) maps them to **ProblemDetails** (RFC 7807) with the right status code (400/404). Never return raw strings for errors.
- **Validation**: guard clauses in Domain (`ArgumentOutOfRangeException.ThrowIfLessThan` etc.); request validation in Application.
- **DI**: each layer exposes one extension method — `AddApplication()`, `AddInfrastructure()`. `Program.cs` only composes.
- **Async**: I/O-shaped abstractions (`ISessionStore`) are async with `CancellationToken`, even if the in-memory implementation is synchronous — so a Redis implementation needs no signature change.
- **Concurrency**: per-session mutation is serialized (per-session `SemaphoreSlim`); never mutate shared state without a lock.
- **Configuration**: options classes bound with `IOptions<T>` (e.g. `SessionOptions { SlidingExpirationMinutes }`). No magic numbers.
- **Logging**: `ILogger<T>` with structured messages (`"Session {SessionId} started with n={N}"`).
- XML doc comments on public types and methods of Domain and Application.

## Testing
- xUnit + FluentAssertions; test project per layer (`*.Domain.Tests`, `*.Application.Tests`, `*.Api.IntegrationTests`).
- Naming: `MethodName_State_ExpectedResult`.
- Arrange / Act / Assert sections separated by blank lines.
- Use `[Theory]` + `[InlineData]` for value ranges.
- Integration tests use `WebApplicationFactory<Program>` (add `public partial class Program;` to Api).

## Commands
```
cd server
dotnet build
dotnet test
dotnet run --project src/CombinationGenerator.Api
```
Before finishing any task: `dotnet build` with zero warnings and `dotnet test` green.
