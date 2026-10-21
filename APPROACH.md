# Approach & Architecture Decisions

This document outlines the architectural approach, thought process, and engineering decisions made across each task in the Northwind Support Desk project.

---

## Task 1: Defect Fix & Server-Side Filtering

### 1. The Defect (DEFECT-117)
- **Symptom:** When a user applied a filter (such as Priority: High), the ticket list did not immediately update or returned inconsistent results when a search term was typed.
- **Root Cause:** In `useTicketList.ts`, the query state for the ticket list hook tracked both immediate filters and a search term. However, the search term was debounced asynchronously, and the query object constructed for `useQuery` was retaining stale parameters or re-triggering requests asynchronously out of sync with instant filter changes.
- **Fix:** Separated immediate filter updates from debounced text search. Immediate filter changes trigger a cache invalidation and fresh fetch immediately with `page = 1`. A dedicated regression test was introduced ensuring priority and other filter changes trigger requests immediately without waiting for keystroke debounce.

### 2. Server-Side Filtering & SQL Verification
- **Predicate Composition:** In `GetTicketsQueryHandler.cs`, filters (`Status`, `Priority`, `CategoryId`, `CustomerId`, `AssignedAgentId`, `UnassignedOnly`, `Search`, and `SlaStatus`) are composed dynamically into the `IQueryable<Ticket>` pipeline before calling `CountAsync()` or applying `.Skip().Take()`.
- **Exact Match vs Case-Insensitive Search:** Priority, Status, and ID filters use exact predicate matching. Customer name and ticket title search use SQL `LIKE` expressions executed by EF Core on SQL Server.
- **SQL Paging Integrity:** Both the total count and the paginated slice are queried from the filtered queryable directly in SQL Server, preventing over-fetching or mismatched page counts in the pager.
- **Adding the Next Filter:** Future filters can be added cleanly by adding the property to `TicketQuery`, creating a corresponding extension or where clause in the query handler, and passing it through from the frontend filter bar.

---

## Task 2: Automatic Triage, Assignment & Escalation

### 1. Triage Engine & SLA Rules
- **Priority Escalation on Triage:** Tickets raised with categories `Security` or `Outage` are automatically triaged to `Critical`, regardless of the requested priority.
- **SLA Calculation:** SLA base windows (Critical: 4h, High: 8h, Medium: 24h, Low: 72h) are evaluated from `CreatedAt`. For `Premium` customers, the window is halved (multiplier `0.5`), but never allowed to fall below the 1-hour floor (`MinimumWindowHours`).
- **Deterministic Auto-Assignment:**
  1. Specialist categories (`Outage`, `Security`, `Billing`) require agents with matching specialties.
  2. Inactive agents (`IsActive == false`) and agents at or above maximum capacity (`OpenTickets >= MaxConcurrentTickets`) are strictly excluded.
  3. The eligible agent with the fewest open tickets wins deterministically (ties broken by agent ID).
  4. If no eligible agent is found, the ticket is created as `Unassigned` with a clear triage reason, returning `201 Created` without failing.

### 2. Ticket Escalation
- **State Invariants:** Tickets already `Critical`, `Resolved`, or `Closed` reject escalation with `409 Conflict`.
- **Recalculation:** On escalation:
  - Priority steps up by one level (Low → Medium → High → Critical).
  - Due date is recomputed from the current time (`DateTime.UtcNow`).
  - Current assignee eligibility is re-evaluated; if no longer eligible or at capacity, the ticket is reassigned or left unassigned.
  - An immutable `TicketEscalationHistory` record is written capturing previous/new priority, previous/new assignee, timestamp, reason, and actor.

### 3. Frontend Implementation
- Created unified `SlaIndicator` component used in both the ticket table and detail views.
- Built accessible `EscalationPanel` with client validation (minimum reason length), pending state, and server error handling.
- Integrated `EscalationHistory` timeline and triage badge components into the ticket detail page.

---

## Task 3: Login and Authentication

### 1. Credential Security & Storage
- **Identity Storage:** Extended `Agent` domain entity with a `PasswordHash` column (max length 256, nullability handled via migration).
- **Hashing Algorithm:** Utilized `PasswordHasher<Agent>` from ASP.NET Core Identity (PBKDF2 with HMAC-SHA512, 100,000 iterations, 128-bit salt), completely avoiding plain text or home-grown hashing.
- **Dev Credentials Fixture:** Seeded all 6 default agents in `SupportDbSeeder.cs` with development password `Password123!`, documented in `.env.example`.

### 2. JWT Issuance & Central Fallback Authorization
- **Token Claims:** Emitted `sub` (Agent ID), `email`, `name`, and `jti` with configurable expiration (default 480 minutes) signed with HMAC-SHA256.
- **Identity Enumeration Defense:** Generic `401 Unauthorized` response with `"Invalid email or password."` for non-existent emails, wrong passwords, and inactive agents (e.g. Yuki Tanaka).
- **Central Authorization Policy:** Applied ASP.NET Core `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`. All existing and future endpoints require a valid Bearer token by default, while anonymous endpoints (`/api/auth/login`, `/health`, Swagger) explicitly opt out via `[AllowAnonymous]`.
- **Actor Attribution:** Extracted agent identity from the JWT `User.Identity.Name` claim in `POST /api/tickets/{id}/escalate`, ensuring `escalatedBy` cannot be spoofed in the request body.

### 3. Frontend Authentication Flow
- **Token Management:** `client.ts` intercepts requests to attach `Authorization: Bearer <token>` and intercepts `401` responses to clear local session storage and trigger an unauthorized callback.
- **Session Context:** `AuthContext.tsx` verifies the token on mount via `GET /api/auth/me`.
- **Protected Routes:** `ProtectedRoute.tsx` guards internal pages, redirecting unauthenticated users to `/login` while saving `location.state.from` so users return to their intended destination upon logging in.
- **UI & Header:** Added `LoginPage.tsx` with form validation, pending state, accessible labels, dev login helper, and a user header with sign-out capability.

---

## Task 4: Dockerisation & Health Probes

### 1. Multi-Stage Dockerfile (`apps/api/Dockerfile`)
- **Build Context:** Built from repository root (`.`).
- **Layer Caching:** Copies `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and all `.csproj` project files first, running `dotnet restore` in an isolated layer. Application code changes only invalidate subsequent compile/publish layers, drastically speeding up local rebuilds.
- **Runtime Image:** Published on `mcr.microsoft.com/dotnet/aspnet:10.0` (Alpine/Debian-chiseled base) rather than the heavy SDK, yielding a lightweight, secure container image.
- **Security:** Operates as non-root user `app` (UID 1654), exposing unprivileged HTTP port `8080`.
- **Build Context Hygiene:** Comprehensive `.dockerignore` excludes `bin/`, `obj/`, `node_modules/`, `.git/`, and frontend assets.

### 2. Compose Architecture & Database Health
- **Service Orchestration:** `docker-compose.yml` configures the `api` service to depend on `sqlserver` with `condition: service_healthy`.
- **Zero Hardcoded Secrets:** Configuration injection via environment variables (`ConnectionStrings__Default`, `Jwt__SecretKey`, `ASPNETCORE_ENVIRONMENT`).
- **Automated Readiness:** Under `ASPNETCORE_ENVIRONMENT=Development`, the API automatically runs database migrations and seeds fixtures on launch.
- **Health Check Probe:** Implemented `GET /health` endpoint that queries `SupportDbContext.Database.CanConnectAsync()`, returning `200 OK {"status":"Healthy"}` or `503 Service Unavailable`, exempt from JWT authentication.
