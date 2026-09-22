# Szamla

Multi-tenant Hungarian invoicing SaaS with NAV Online Számla integration. Backend: .NET 10 /
ASP.NET Core Web API, Clean Architecture, PostgreSQL. Frontend (Phase 6, not started yet): React
+ TypeScript.

Project language conventions: code, comments, and technical docs (like this one) are in English.
The UI and invoice documents are Hungarian, with the domain layer built for later i18n.

## Status

Phase 4 of the plan ("Számla-életciklus"): invoice draft → finalize → storno/modification,
persistence, audit logging, and PDF generation. See [Phase 2](#phase-2-notes),
[Phase 3](#phase-3-notes) and [Phase 4](#phase-4-notes) notes below for what's in place and
what's deliberately deferred.

Verified end-to-end against real PostgreSQL (Docker): all 96 tests pass (`dotnet test`,
including the Testcontainers-backed ones — among them a 50-way-concurrent test proving the
invoice numbering never collides or skips), the migrations apply cleanly, and a full HTTP flow
against the Dockerized API was confirmed with `curl`: register-tenant → login → create partner →
create invoice series → create draft invoice → finalize → storno → finalize the storno → list →
fetch as JSON and as PDF.

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

## Phase 3 notes

What's built (all in `Szamla.Domain`, unless noted):
- `Money` — a currency-tagged decimal value object; arithmetic between different currencies
  throws `CurrencyMismatchException` rather than silently mixing them.
- `RoundingPolicy` — the single, documented place for every rounding rule: line/VAT-rate amounts
  keep 2 decimals; only the invoice's HUF grand total (and the HUF-converted VAT amount on
  foreign-currency invoices) round to whole forints, using away-from-zero ("kerekítés") rounding,
  not banker's rounding.
- `VatRate` / `VatExemptionReason` — either a percentage (27/18/5/0%) or one of the exemption
  reasons named in the brief (AAM, TAM, EUE, EUFAD37, belföldi fordított adózás, területi
  hatályon kívüliség). **The exact NAV Online Számla XSD field/enum names for these are not yet
  verified** — that happens in Phase 5 against the official schema; see the doc comment on
  `VatExemptionReason`.
- `InvoiceLine` — computes net/VAT/gross per line from quantity × unit price and a `VatRate`.
- `Invoice` — the aggregate: header, immutable `IssuerSnapshot`/`PartnerSnapshot` (so a later edit
  to the tenant's or partner's profile can't retroactively change an issued invoice), lines,
  computed totals, and the mandatory per-VAT-rate breakdown table (`VatSummary`). This phase
  covers the data shape and VAT arithmetic only — `Status`/`Number` exist per the agreed data
  model, but the lifecycle transitions that set them (draft → finalize, storno/modification
  referencing the original) are Phase 4 work, once there's a persistence layer to drive them.
- `Partner` — buyer entity with the domestic/EU/third-country split from the brief; a domestic,
  non-private-person buyer requires a tax id (further, threshold-based mandatory-tax-number rules
  are noted but not yet enforced — flagged for legal verification alongside Phase 4).
- **Sequential numbering**, proven concurrency-safe: `InvoiceSeries` (Domain) plus
  `InvoiceNumberCounter` and `InvoiceNumberGenerator` (Infrastructure), which allocates numbers
  via a single atomic PostgreSQL `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` — no explicit
  lock or transaction needed, Postgres serializes concurrent writers on the same counter row by
  itself. `InvoiceNumberGeneratorConcurrencyTests` fires 50 concurrent allocation requests at one
  (tenant, series, year) and asserts the results are exactly `{1..50}` — no duplicates, no gaps.
- `Product` (termék/szolgáltatás törzs) was **not** added this phase — it wasn't named in the
  Phase 3 scope ("számla, tétel, partner, Money, ÁFA-számítás, sorszámozás") and `InvoiceLine`
  doesn't need it to compute correctly; it'll come with the catalog feature alongside Phase 4.

Deliberately deferred to Phase 4 ("Számla-életciklus"):
- Draft → finalize → storno/modification transitions on `Invoice` (the fields exist; the methods
  that mutate them don't yet).
- Persistence (EF mapping, migrations, tenant query filters) for `Invoice`, `InvoiceLine`, and
  `Partner` — only the numbering infrastructure needed a real database this phase.
- Audit logging and PDF generation.

## Phase 4 notes

What's built:
- **Persistence for Invoice/InvoiceLine/Partner** (deferred from Phase 3): `IssuerSnapshot`/
  `PartnerSnapshot`/`Money` are mapped via EF Core's `ComplexProperty` (the value-type-friendly
  feature introduced in EF8 — plain `OwnsOne`/`OwnsMany` reject `Money` outright since it's a
  struct). `InvoiceLine` is mapped as a **regular entity** with a shadow FK back to `Invoice`,
  not as an EF Core owned type: owned types can't currently host `ComplexProperty` members, which
  every Money-typed property on a line needs. `Partner` and `InvoiceSeries` both get the
  `HasQueryFilter` tenant isolation `Users` was deliberately excluded from in Phase 2 — proven by
  `InvoicePersistenceTests`.
- **Invoice lifecycle** on the aggregate itself: `ReplaceLines`/`UpdateHeader` (draft-only),
  `Finalize` (assigns the number, irreversible), and the `CreateStorno`/`CreateModification`
  static factories, all enforced structurally (`EnsureDraft()` throws once `Status` is
  `Finalized` — there's no code path back to Draft).
- **Two real EF Core bugs found and fixed** while wiring the above up, both regression-tested:
  1. Editing a draft's lines (`ReplaceDraftInvoiceLinesCommandHandler`) generated an `UPDATE`
     against a row that had never been `INSERT`ed, because `InvoiceLine`'s key is a
     client-assigned `Guid` — reached only through navigation fixup on an already-tracked
     `Invoice`, a brand new line looks identical to "existing row, just edited" to EF Core. Fixed
     by adding lines/removing lines through `IApplicationDbContext.InvoiceLines` explicitly
     instead of relying on graph-fixup inference (see the interface's doc comment).
  2. `Invoice.CreateStorno`/`CreateModification` passed the *original's own* `Issuer`/`Partner`
     instances to the new invoice. Since those are owned complex types keyed by their owning
     Invoice's id, sharing one instance between two Invoice rows threw
     `"...is part of a key and so cannot be modified..."`. Fixed with `original.Issuer with { }`
     (records' copy syntax) — regression-tested in `InvoiceTests` via `ReferenceEquals`.
- **Audit logging**: `AuditSaveChangesInterceptor`, an EF Core `SaveChangesInterceptor` that
  writes one `AuditLog` row (who via the JWT's claims, when, what entity/id, before/after JSON of
  scalar properties) per Added/Modified/Deleted change to `Tenant`/`Partner`/`Invoice`/
  `InvoiceSeries`, in the *same* `SaveChanges` call as the change itself — proven by
  `AuditSaveChangesInterceptorTests`. ASP.NET Core Identity's own tables are deliberately excluded
  (not "business data," and every login would otherwise spam the log via `RefreshToken` rotation).
- **Application layer**: CQRS commands/queries for Partners (Create/Update/Get/List) and Invoices
  (CreateDraft/ReplaceLines/Finalize/CreateStorno/CreateModification/Get/List), plus
  CreateInvoiceSeries. `FinalizeInvoiceCommandHandler` ties the number generator to the invoice's
  chosen series and its `IssueDate.Year`.
- **API**: `PartnersController`, `InvoicesController`, `InvoiceSeriesController` — see the
  endpoint list in the Phase 1 architecture notes (all now real, wired to the handlers above).
  `GET /api/invoices/{id}/pdf?copy=true` returns the "MÁSOLAT"-watermarked copy.
- **PDF generation**: `InvoicePdfGenerator` (QuestPDF) renders the issuer/partner blocks, dates,
  line-item table, the mandatory per-VAT-rate breakdown table, and totals (with the HUF-converted
  VAT figure for foreign-currency invoices). This is a functional, unbranded MVP layout — **a
  legal completeness review of the exact required wording and any case-specific mandatory fields
  is still owed**, consistent with not trusting memory on legal specifics; see the doc comment on
  `InvoicePdfGenerator`.

Role-based authorization: mutating endpoints (create/edit/finalize/storno/modify) require
`Roles.CanWrite` (Owner/Admin/Invoicer — not ReadOnly/"csak olvasó"); invoice-series setup
requires the narrower `Roles.CanManageSettings` (Owner/Admin only). Reads stay open to any
authenticated role. This relies on ASP.NET Core's own `[Authorize(Roles = ...)]` middleware, so
it isn't covered by a dedicated automated test here — there's no "invite a ReadOnly user" endpoint
yet to set one up with in a test, and the framework mechanism itself isn't code this project owns.

Deliberately deferred:
- Delete endpoints, list pagination/filtering beyond a basic status filter and name search.
- Email sending and the 23/2014 NGM tax-audit data export — both explicitly out of this phase's
  scope already (email is a Phase 5 background job per the brief; the export format needs the
  legal verification the brief itself calls for).
- `Product` (termék törzs) — still not needed; `InvoiceLine` takes free-text description/price.

### Licensing flags for the maintainer to keep an eye on

- **QuestPDF** (PDF generation, Phase 4): already discussed — Community (free) license applies
  since company revenue is under its threshold. Re-check questpdf.com's licensing page before
  each major version bump, since thresholds and terms can change.
- **FluentAssertions** (test assertions, all test projects, resolved at v8.11.0): v8+ requires a
  paid Xceed license above a revenue threshold, with a free tier below it. Confirmed with the
  client (same as QuestPDF) that current revenue is under that threshold. Re-check
  fluentassertions.com/licensing before a major version bump or once revenue approaches it.
