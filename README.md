# Szamla

Multi-tenant Hungarian invoicing SaaS with NAV Online Számla integration. Backend: .NET 10 /
ASP.NET Core Web API, Clean Architecture, PostgreSQL. Frontend (Phase 6, not started yet): React
+ TypeScript.

Project language conventions: code, comments, and technical docs (like this one) are in English.
The UI and invoice documents are Hungarian, with the domain layer built for later i18n.

## Status

Phase 2 of the plan ("alapok"): solution structure, Docker, database, authentication,
multi-tenancy foundation, CI. See [Phase 2 notes](#phase-2-notes) below for what's in place and
what's deliberately deferred to later phases.

Verified end-to-end against real PostgreSQL (Docker): all 17 tests pass (`dotnet test`,
including the Testcontainers-backed ones), the `InitialCreate` migration applies cleanly, and
`docker compose up --build` produces a working API container — register-tenant, login and
health checks all confirmed with `curl` against the running container.

## Solution layout

```
src/
  Szamla.Domain          - entities, value objects; no dependency on anything else
  Szamla.Application     - use cases (a small hand-rolled CQRS dispatcher), interfaces Infrastructure implements
  Szamla.Infrastructure  - EF Core, PostgreSQL, ASP.NET Core Identity, JWT issuing
  Szamla.Api             - ASP.NET Core Web API host, controllers, Swagger
tests/
  Szamla.Domain.Tests         - unit tests, no external dependencies
  Szamla.Application.Tests    - unit tests (EF Core InMemory provider stands in for persistence)
  Szamla.Infrastructure.Tests - integration tests against a real PostgreSQL (Testcontainers)
  Szamla.Api.Tests            - integration tests: HealthCheckTests need no DB, AuthFlowTests need Docker
docker/
  docker-compose.yml, Szamla.Api.Dockerfile, .env.example
```

Dependency direction: `Api -> Application, Infrastructure`; `Infrastructure -> Application`;
`Application -> Domain`. Domain has zero framework dependencies.

## Prerequisites

- .NET 10 SDK
- PostgreSQL 17 (locally, or via `docker/docker-compose.yml`)
- Docker, to run `docker-compose` and the Testcontainers-based integration tests
  (`Szamla.Infrastructure.Tests`, and `AuthFlowTests` in `Szamla.Api.Tests`)

## Running locally

0. `appsettings.Development.json` is git-ignored (it holds a signing key, even if a dev-only
   placeholder one — see "never commit secrets" below). Create your own from the template:
   `cp src/Szamla.Api/appsettings.Development.json.example src/Szamla.Api/appsettings.Development.json`.
1. Start PostgreSQL. Either your own instance matching that file's `ConnectionStrings:DefaultConnection`
   (`Host=localhost;Port=5432;Database=szamla;Username=szamla;Password=szamla_dev_only` by default), or:
   ```
   cd docker
   cp .env.example .env   # fill in POSTGRES_PASSWORD and JWT_SIGNING_KEY
   docker compose up postgres -d
   ```
2. Apply migrations:
   ```
   dotnet tool restore
   dotnet tool run dotnet-ef database update --project src/Szamla.Infrastructure --startup-project src/Szamla.Infrastructure
   ```
3. Run the API:
   ```
   dotnet run --project src/Szamla.Api
   ```
   Swagger UI is at `/swagger` in Development. Liveness: `GET /health/live`. Readiness (checks
   the database): `GET /health/ready`.

To run the whole stack (API + PostgreSQL) in Docker: `docker compose -f docker/docker-compose.yml up --build`
(after filling in `docker/.env`). The frontend service will be added in Phase 6.

**The signing key and password in `appsettings.Development.json` are placeholders for local
development only — never reuse them anywhere real.** Production values come from environment
variables / `.env` (`docker/.env`, git-ignored, based on `docker/.env.example`).

## Tests

```
dotnet test
```

`Szamla.Domain.Tests` and `Szamla.Application.Tests` need nothing external and always run.
`Szamla.Infrastructure.Tests` and the Testcontainers-backed tests in `Szamla.Api.Tests` need
Docker; without it they fail fast with `DockerUnavailableException` rather than hanging. GitHub
Actions (`.github/workflows/ci.yml`) runs the full suite, since `ubuntu-latest` runners have
Docker preinstalled.

## Phase 2 notes

What's built:
- Solution/project structure per Clean Architecture, wired together.
- `Tenant` domain entity with unit tests (Phase 3 will add the invoicing domain proper: Invoice,
  InvoiceLine, Money, VAT calculation, sequential numbering).
- A minimal hand-rolled CQRS dispatcher (`ISender`/`ICommand`/`IQuery` + handlers, resolved via
  DI) instead of MediatR — MediatR's licensing terms changed in 2025 (see below); this mechanism
  is small enough not to need a package.
- `RegisterTenant` use case (Application) + the `/api/auth/register-tenant` endpoint (API),
  which also creates the tenant's owner login (ASP.NET Core Identity) in the same DB transaction.
- JWT authentication: login, refresh-token rotation, logout, and TOTP-based two-factor
  authentication (enable / confirm / verify-at-login), all under `/api/auth/*`.
- PostgreSQL via EF Core, with the first migration (`InitialCreate`) generated and verified
  (applies cleanly against a fresh database, both via Testcontainers and via `docker compose`).
- Multi-tenancy foundation: `ICurrentTenantService` (reads the `tenant_id` JWT claim), used going
  forward for `HasQueryFilter` on tenant-scoped business entities from Phase 3 on. **`Users` is
  deliberately not covered by a global query filter** — see the comment on the `ApplicationUser`
  mapping in `ApplicationDbContext` for why (login and Identity's own email-uniqueness check both
  need to look across tenants). Tenant isolation for `Users` is instead enforced explicitly at
  the query site and proven by `TenantIsolationTests`.
- GitHub Actions CI: restore, build, test on every push/PR to `main`.

Deliberately deferred:
- No business/invoicing endpoints yet (partners, products, invoices) — Phase 3+.
- No Hangfire/Quartz background jobs yet — introduced with NAV submission in Phase 5.
- `Szamla.Infrastructure.Nav` (the NAV Online Számla client) does not exist yet; it's created in
  Phase 5, once the official XSDs and interface description have been reviewed.
- ASP.NET Core Data Protection for encrypting NAV technical-user secrets is deferred to Phase 5,
  when there's something to encrypt.

### Licensing flags for the maintainer to keep an eye on

- **QuestPDF** (PDF generation, Phase 4): already discussed — Community (free) license applies
  since company revenue is under its threshold. Re-check questpdf.com's licensing page before
  each major version bump, since thresholds and terms can change.
- **FluentAssertions** (test assertions, all test projects, resolved at v8.11.0): v8+ requires a
  paid Xceed license above a revenue threshold, with a free tier below it. Confirmed with the
  client (same as QuestPDF) that current revenue is under that threshold. Re-check
  fluentassertions.com/licensing before a major version bump or once revenue approaches it.
