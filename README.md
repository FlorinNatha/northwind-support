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
  - Tickets with `Security` or `Outage` categories automatically elevate to `Critical` priority upon creation.
  - SLA due dates are calculated from creation time using priority windows (Critical: 4h, High: 8h, Medium: 24h, Low: 72h). Premium customer windows are halved (50% reduction) with a strict 1-hour minimum floor.
  - Deterministic auto-assignment: specialist categories (`Outage`, `Security`, `Billing`) require matching agent specialties; inactive agents and agents at maximum capacity (`OpenTickets >= MaxConcurrentTickets`) are excluded; the eligible agent with the fewest open tickets wins deterministically (ties broken by agent ID). If none are eligible, the ticket remains unassigned with a 201 Created response.
- **Ticket Escalation:**
  - Priority steps up by one level (Low → Medium → High → Critical). Tickets already `Critical`, `Resolved`, or `Closed` return `409 Conflict`.
  - Due date is recomputed from the current timestamp (`UtcNow`).
  - Assignee eligibility is re-evaluated and re-assigned if the current agent is no longer eligible.
  - An immutable `TicketEscalationHistory` record is persisted capturing before/after priority, assignee, timestamp, reason, and actor.
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

4. **Database Migrations on Container Startup:**
   - *Chosen:* In Development (`ASPNETCORE_ENVIRONMENT=Development`), the API executes `MigrateAndSeedAsync()` at startup.
   - *Why:* Keeps local developer onboarding and evaluation one command away from a running system (`docker compose up`).
   - *Real Deployment Approach:* In production, running migrations in application web processes is an anti-pattern because multiple concurrent container instances can race or deadlock on migration locks, and application runtime identities should not have DDL (table alter/create) database privileges. In production, migrations should be applied as an isolated CI/CD deployment step, a Kubernetes `initContainer`, or an ephemeral migration job using elevated deployment credentials before routing traffic to web instances.

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

## Quick Start & Running the Project

### Option A: Local Development (Bare Metal API + React)

```bash
# 1. Start SQL Server in Docker
npm run db:up

# 2. Run API (.NET 10) in Terminal 1
npm run api

# 3. Run Web App (Vite) in Terminal 2
npm run web
```

- Web UI: http://localhost:5173
- API: https://localhost:7043 (http://localhost:5043)
- Swagger: https://localhost:7043/swagger

---

### Option B: Containerised API via Docker Compose (Task 4)

```bash
# 1. Build and start the whole stack (SQL Server + API)
docker compose up --build -d

# 2. Start the web app pointing at the containerised API
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

#### Task 4 Verification Checklist:
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

## Running the Automated Test Suites

```bash
# Runs both .NET unit/architecture tests and Web Vitest test suites
npm test
```

- **Backend:** 130 Unit Tests + 4 Architecture Tests (134 total passing, 0 failing)
- **Frontend:** 28 Unit / Component Tests (all 28 passing, 0 failing)
- **Code Quality:** `npm run lint` and `npm run typecheck` pass with 0 errors and 0 warnings.

---

## Known Limitations & Out of Scope

As specified in the assignment constraints, the following features were intentionally excluded from this iteration and should be implemented in future phases:
- **Identity & Account Management:** Self-registration, email verification, password reset workflows, and account lockout after repeated failed attempts.
- **Advanced Auth:** Multi-factor authentication (MFA), OAuth2/OIDC social logins, and refresh token rotation with sliding expiration.
- **Role-Based Access Control (RBAC):** Fine-grained permission matrices (e.g. Lead Agent vs Regular Agent permissions).
- **Production Infrastructure:** CI/CD deployment pipelines, Helm charts / Kubernetes manifests, HTTPS termination inside containers, and external secret stores (Vault / Azure Key Vault).
