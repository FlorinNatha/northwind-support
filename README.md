# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents, track SLA compliance, escalate priorities, and move tickets through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server 2022**, in one monorepo.

---

## What Was Implemented

### Task 1: Defect Fix & Server-Side Filtering
- **DEFECT-117 Fixed:** Resolved an asynchronous state mismatch in `useTicketList.ts` where debounced search parameter updates interfered with immediate filter dropdowns. Filter changes now immediately invalidate the query cache and reset to page 1.
- **Server-Side SQL Filtering:** All filter dimensions (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`, and `slaStatus`) are evaluated in SQL via EF Core predicates before paging and counting. Total counts accurately reflect filtered totals.

### Task 2: Triage Engine, SLA Compliance & Escalation
- **Automatic Triage:**
  - Tickets with `Security` or `Outage` categories automatically elevate to `Critical` priority upon creation (BR-1).
  - SLA due dates are calculated from creation time using priority windows (Critical: 4h, High: 8h, Medium: 24h, Low: 72h). Premium customer windows are halved (50% reduction) with a strict 1-hour minimum floor (BR-2, BR-3).
  - Deterministic auto-assignment: specialist categories (`Outage`, `Security`, `Billing`) require matching agent specialties; inactive agents and agents at maximum capacity (`OpenTickets >= MaxConcurrentTickets`) are excluded; the eligible agent with the fewest open tickets wins deterministically (ties broken by agent ID) (BR-4, BR-5). If none are eligible, the ticket remains unassigned with a 201 Created response explaining the reason (BR-6).
- **Ticket Escalation:**
  - Priority steps up by one level (Low → Medium → High → Critical). Tickets already `Critical`, `Resolved`, or `Closed` return `409 Conflict`.
  - Due date is recomputed from the current timestamp (`UtcNow`).
  - Assignee eligibility is re-evaluated and re-assigned if the current agent is no longer eligible.
  - An immutable `TicketEscalation` record is persisted capturing before/after priority, assignee, timestamp, reason, and actor (BR-7).
- **Frontend Features:** Unified `SlaIndicator` component, interactive `EscalationPanel` with client-side validation and server error feedback, escalation history audit timeline, and triage status indicators.

### Task 3: Login and Authentication
- **Secure Password Hashing:** Stored in `Agents.PasswordHash` using `PasswordHasher<Agent>` from ASP.NET Core Identity (PBKDF2 with HMAC-SHA512, 100,000 iterations, 128-bit salt).
- **JWT Token Issuance:** `POST /api/auth/login` issues signed tokens (HMAC-SHA256, 480-minute lifetime) containing `sub`, `email`, `name`, and `jti` claims.
- **Identity Enumeration Defense:** Invalid emails, incorrect passwords, and inactive agents (Yuki Tanaka) all return an identical `401 Unauthorized` with `"Invalid email or password."`.
- **Central Authorization Policy:** Configured via `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`. All existing and future endpoints require a valid token by default; `/api/auth/login`, `/health`, and Swagger opt out via `[AllowAnonymous]`.
- **Token Attribution:** `POST /api/tickets/{id}/escalate` derives `escalatedBy` from the JWT claims rather than trusting client-provided bodies.
- **Client Auth Handling:** `client.ts` automatically attaches `Authorization: Bearer <token>` and intercepts 401s to clear the session and redirect to `/login`. Added `LoginPage.tsx`, `ProtectedRoute.tsx` (preserving destination route), and an active agent profile with logout in the navigation header.

### Task 4: Dockerise the API
- **Multi-Stage Dockerfile (`apps/api/Dockerfile`):** Built with `.NET 10 SDK` and published to `mcr.microsoft.com/dotnet/aspnet:10.0`. Project files and centralized package files are copied and restored first to cache the restore layer.
- **Non-Root Execution:** Runs as unprivileged user `app` (UID 1654) listening on port 8080.
- **Docker Compose Orchestration:** `api` service runs on `localhost:5080`, waiting on `sqlserver` healthcheck readiness.
- **Health Check (`GET /health`):** Probes database connectivity via EF Core `CanConnectAsync()`, publicly accessible without authentication.
- **Environment Driven:** Zero baked credentials; configuration is injected through environment variables.

---

## Design Decisions: What We Chose, What We Rejected, and Why

1. **Credentials Storage on `Agent` vs Separate `Users` Table:**
   - *Chosen:* Added a `PasswordHash` column directly to the existing `Agent` entity.
   - *Why:* The business model specifies that support agents *are* the users. A 1:1 separate `Users` table would introduce unnecessary joins and foreign key synchronization overhead without added security benefits for this system.
   - *Rejected:* A separate identity table or full ASP.NET Core Identity schema. The overhead of ~8 Identity tables (roles, claims, tokens) was disproportionate for internal agent credentials.

2. **Central Fallback Authorization Policy vs Controller `[Authorize]` Attributes:**
   - *Chosen:* Configured `FallbackPolicy` in `Program.cs` to require authenticated users globally.
   - *Why:* "Secure by default" prevents human error. Developers cannot accidentally expose new endpoints when adding controllers or actions; public endpoints must explicitly opt out via `[AllowAnonymous]`.
   - *Rejected:* Decorating every controller with `[Authorize]`. Fragile and prone to accidental omissions during team expansion.

3. **Client-Side Token Storage in `localStorage` vs `HttpOnly` Cookies:**
   - *Chosen:* `localStorage` with header attachment via Axios client interceptor.
   - *Trade-off:* `localStorage` is susceptible to XSS if malicious scripts execute in the browser context, but immune to CSRF. `HttpOnly` cookies protect against XSS token theft, but introduce CSRF risks, CORS cookie credentials handling, and same-site configuration complexity across separate frontend/backend hostnames. Given the separate dev servers and container port mappings, `localStorage` provided clean, predictable token management.
   - *Production Recommendation:* In production, an OAuth2/OIDC Authorization Code Flow with PKCE terminating at a Backend-for-Frontend (BFF) proxy storing session cookies with `HttpOnly`, `SameSite=Strict`, and `Secure` attributes would provide defense in depth against both XSS and CSRF.

4. **EscalatedBy Attribution (Task 3 Completion):**
   - *Chosen:* Derived exclusively from the authenticated JWT claims in `EscalateTicketCommandHandler` using `IUserContext` / `HttpContext.User`.
   - *Why:* We ended up with server-enforced token attribution as instructed in AU-12 and Section 2.3.B. A client-supplied `escalatedBy` in the request body can be forged; extracting the caller's identity directly from the signed JWT guarantees audit trail integrity.

5. **Database Migrations on Container Startup:**
   - *Chosen:* In Development (`ASPNETCORE_ENVIRONMENT=Development`), the API executes `MigrateAndSeedAsync()` at startup.
   - *Why:* Keeps local developer onboarding and evaluation one command away from a running system (`docker compose up`).
   - *Real Deployment Approach:* In production, running migrations in application web processes is an anti-pattern because multiple concurrent container instances can race or deadlock on migration locks, and application runtime identities should not have DDL (table alter/create) database privileges. In production, migrations should be applied as an isolated CI/CD deployment step, a Kubernetes `initContainer`, or an ephemeral migration job using elevated deployment credentials before routing traffic to web instances.

---

## The Defect (DEFECT-117)

- **Root Cause:** In `useTicketList.ts`, the hook managed both instantaneous filter dropdowns and text search in a coupled state object. Because the search parameter was asynchronously debounced, changing a filter dropdown either sent no immediate request or maintained stale filter parameters in React Query until search debounce settled.
- **The Fix:** Separated immediate filter selection from text search. Dropdown filter changes instantly update query state, reset the pagination to `page: 1`, and trigger an immediate fetch without waiting for keystroke debounce. Text search remains debounced (300ms) to avoid request flooding.
- **How to Verify:**
  - Automated: Run `npm test` to execute `apps/web/src/features/tickets/hooks/useTicketList.test.ts`. The regression test asserts that changing Priority sends an immediate request with `priority=High` without delay.
  - Manual: Open the ticket list at `http://localhost:5173/`, select a priority (e.g. *High*), and verify in browser DevTools Network tab that an immediate API request is fired and only High tickets are rendered.

---

## Filtering Architecture

- **Where Filters are Applied:** All filters are evaluated in SQL inside `SupportDesk.Infrastructure.Queries.TicketQueries.ApplyFilters` using EF Core `IQueryable<TicketWithLabels>`. Predicates (`Status`, `Priority`, `CategoryId`, `CustomerId`, `AssignedAgentId`, `UnassignedOnly`, `Search`, and `SlaStatus`) are appended before `CountAsync` and pagination (`Skip`/`Take`).
- **How We Checked the SQL:** Verified via ASP.NET Core console SQL logging (`Microsoft.EntityFrameworkCore.Database.Command` set to `Information` in `appsettings.Development.json`). We confirmed that queries produce parameterized SQL `WHERE` clauses, that `Search` translates to SQL `LIKE`, and that `slaStatus` translates to SQL Server `DATEDIFF(second, ...)` without client-side evaluation warnings.
- **How the Next Filter Gets Added:**
  1. **Contract:** Add the optional filter property to `TicketQuery.cs` in `SupportDesk.Application.Contracts.Tickets`.
  2. **Query Pipeline:** In `TicketQueries.ApplyFilters`, add `if (query.MyFilter.HasValue) { source = source.Where(x => ...); }`.
  3. **Web API Client:** In `apps/web/src/features/tickets/api.ts`, add the property to `TicketFilters` interface and pass it to `URLSearchParams`.
  4. **UI Filter Bar:** Add the corresponding input or dropdown control to `TicketFilterBar.tsx`.

---

## Database Schema & Indexing Strategy

### Schema Additions
1. **`Agents` Table:** Added `PasswordHash NVARCHAR(MAX) NULL` to store PBKDF2 hashed credentials.
2. **`TicketEscalations` Table:** Added table storing immutable escalation audit records (`Id`, `TicketId`, `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `FromDueAtUtc`, `ToDueAtUtc`, `Reason`, `EscalatedBy`, `EscalatedAtUtc`).

### Indexes Added & Why
- **`IX_TicketEscalations_TicketId`:** Foreign key index on `TicketId` to ensure optimal performance when deleting or inspecting tickets with cascading foreign keys.
- **`IX_TicketEscalations_TicketId_EscalatedAtUtc`:** Composite index on `(TicketId, EscalatedAtUtc DESC)` configured in `TicketEscalationConfiguration.cs`.
  - *Why:* The endpoint `GET /api/tickets/{id}/escalations` queries escalation history for a specific ticket ordered newest first (`ORDER BY EscalatedAtUtc DESC`). This composite index allows SQL Server to seek directly to the ticket's history and scan in reverse index order, eliminating expensive sort operations (`Sort` in execution plans).

---

## How to Log In (Development Credentials)

All seeded agents share the development fixture password:
**`Password123!`**

| Agent Name | Email | Specialties | Status / Expected Login |
| --- | --- | --- | --- |
| **Alex Turner** | `alex.turner@northwind-support.example` | General, Technical | Active (200 OK) |
| **Sara Lindqvist** | `sara.lindqvist@northwind-support.example` | General, Billing | Active (200 OK) |
| **Priya Nair** | `priya.nair@northwind-support.example` | Outage, Security | Active (200 OK) |
| **David Kim** | `david.kim@northwind-support.example` | Technical, Outage | Active (200 OK) |
| **Maria Garcia** | `maria.garcia@northwind-support.example` | Billing, General | Active (200 OK - at capacity) |
| **Yuki Tanaka** | `yuki.tanaka@northwind-support.example` | Technical | Inactive (**401 Login Rejected**) |

---

## Docker & Containerisation (Task 4)

### Building and Running the Stack

```bash
# 1. Build and start the whole stack (SQL Server + API)
docker compose up --build -d

# 2. Run the web app pointing at the containerised API
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

### Environment Variables

| Variable | Service | Description | Example / Default |
| --- | --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | `api` | SQL Server connection string | `Server=sqlserver,1433;Database=SupportDesk;User Id=sa;...` |
| `Jwt__SigningKey` | `api` | HMAC-SHA256 256-bit signing key | Configured in `docker-compose.yml` & `.env.example` |
| `Jwt__Issuer` | `api` | Valid JWT token issuer | `SupportDesk` |
| `Jwt__Audience` | `api` | Valid JWT token audience | `SupportDesk.Client` |
| `ASPNETCORE_ENVIRONMENT` | `api` | ASP.NET hosting environment | `Development` |
| `VITE_API_PROXY_TARGET` | `web` | Vite reverse proxy destination | `http://localhost:5080` |

### Task 4 Verification Checklist:
1. **Health Check Probe:**
   ```bash
   curl -i http://localhost:5080/health
   # Returns: HTTP/1.1 200 OK {"status":"Healthy","database":"Connected"} without a token
   ```
2. **Container Image Size:**
   ```bash
   docker image ls northwind-support-api
   # Runtime image is ~125 MB (vs 800+ MB SDK image)
   ```
3. **Non-Root User Check:**
   ```bash
   docker compose exec api whoami
   # Prints: app (UID 1654, NOT root)
   ```
4. **Clean Startup against Empty Volume:**
   ```bash
   docker compose down -v && docker compose up --build -d
   ```

---

## Testing Strategy

### How to Run the Tests

```bash
# Runs both .NET unit/architecture tests and Web Vitest test suites
npm test
```

### What We Chose to Test
- **Core Domain Logic & SLA Boundaries:** Triage priority enforcement (Outage/Security → Critical), customer tier window halving with strict 1-hour minimum floor, deterministic least-loaded agent assignment, capacity exclusion (`OpenTickets >= MaxConcurrentTickets`), and state transition constraints (409 on Critical/Resolved/Closed).
- **Authentication & Security:** Generic 401 error parity (preventing identity enumeration for invalid email, wrong password, or inactive agent), password hashing/salting verification, and JWT claim extraction.
- **Database Query Filtering:** SQL predicate filtering across multiple criteria (`status`, `priority`, `slaStatus`, `customer`, `agent`, and `search`), boundary comparisons, and accurate total counts.
- **Frontend Interaction & Edge Cases:** Validation on escalation reason length (blocking requests under 5 characters), 409 conflict feedback banners, accessible multi-state `SlaIndicator` rendering without relying on color alone, and protected route redirection preserving target URLs.

### What We Deliberately Did Not Test
- **Framework & Infrastructure Wiring:** We avoided testing EF Core internal mechanics, standard ASP.NET Core middleware pipeline mechanics, or database engine drivers.
- **Trivial POCOs and DTOs:** Plain DTOs, getters, and setters without logic are omitted to maintain a high signal-to-noise ratio in test suites.

---

## AI Usage

In compliance with the assignment instructions, below is an honest account of our AI tooling and workflow:

- **Tools Used and For What:**
  - AI Assistant (Antigravity / Claude) was used for: accelerating initial boilerplate DTO creation, scaffolding xUnit test permutation matrices for SLA matrix rules, advising on Docker multi-stage build optimization, and TypeScript interface declarations.
- **Workflow / Prompt That Worked Well:**
  - Providing exact business rules and domain requirements upfront before generating code (e.g., *"Generate comprehensive xUnit tests for SLA calculation covering Standard vs Premium customer tiers, 1-hour floor boundaries, and edge cases where priority is forced to Critical"*).
- **Something AI Generated That Was Accepted As-Is:**
  - Standard DTO record definitions, regular expressions for email and input validation, and the multi-stage Dockerfile layer structure.
- **Something AI Generated That Was Changed or Rejected, and Why:**
  1. *Central Fallback Policy vs Controller Attributes:* AI initially suggested decorating every controller action with `[Authorize]`. We rejected this approach in favor of ASP.NET Core's global `FallbackPolicy` in `Program.cs`. Controller-level attributes are fragile and easily forgotten when new endpoints are introduced; a global fallback policy guarantees that the API is secure by default.
  2. *EscalatedBy Body Parameter:* AI generated an escalation endpoint that trusted an `escalatedBy` field sent in the client JSON body. We rejected this in favor of extracting the authenticated identity directly from the validated JWT claims (`User.FindFirstValue(ClaimTypes.NameIdentifier)`), eliminating identity spoofing.
  3. *In-Memory SLA Filtering:* AI initially suggested evaluating `slaStatus` using LINQ in-memory after fetching tickets. We rejected this because the assignment explicitly required all filtering, counting, and paging to execute in SQL; we implemented `EF.Functions.DateDiffSecond` in EF Core instead.
- **Bugs AI Introduced or Helped Find:**
  - AI initially omitted the 1-hour minimum SLA floor for high-priority tickets belonging to premium customers, which our xUnit boundary tests caught and corrected.
  - AI helped identify that `DueAtUtc` is nullable for unassigned/open legacy tickets, preventing potential null reference exceptions during SQL query evaluation.
- **How Generated Code Was Verified:**
  - Ran full automated test suites (134 backend tests, 28 frontend tests).
  - Inspected generated SQL in the API console output to ensure predicates were evaluated on SQL Server.
  - Executed end-to-end HTTP requests via `curl.exe` and browser DevTools against live Docker containers.

---

## Known Limitations & Out of Scope

In accordance with the assignment guidelines, the following features were intentionally excluded from this iteration:
- **Identity & Account Management:** Self-registration, email verification, password reset workflows, and account lockout after repeated failed attempts.
- **Advanced Auth:** Multi-factor authentication (MFA), OAuth2/OIDC social logins, and refresh token rotation with sliding expiration.
- **Role-Based Access Control (RBAC):** Fine-grained permission matrices (e.g. Lead Agent vs Regular Agent permissions).
- **Production Infrastructure:** CI/CD deployment pipelines, Helm charts / Kubernetes manifests, HTTPS termination inside containers, and external secret stores (Vault / Azure Key Vault).
