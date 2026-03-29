# 06 - Build Order

This document describes the recommended phase-by-phase implementation order.
Each phase produces working, testable, committable code.

---

## Phase 1: Solution Scaffold

**Goal:** Restructure the solution and create the three projects before writing any domain code.

### Steps

1. **Rename existing project folder** from `PedalAcrossCanada/` to `PedalAcrossCanada.Client/` and update `.csproj` accordingly.
2. **Create `PedalAcrossCanada.Server`** - ASP.NET Core Web API project targeting `net10.0`.
3. **Create `PedalAcrossCanada.Shared`** - Class library targeting `net10.0`.
4. **Update solution file** to include all three projects.
5. Add project references:
   - `PedalAcrossCanada.Client` references `PedalAcrossCanada.Shared`
   - `PedalAcrossCanada.Server` references `PedalAcrossCanada.Shared`
6. Install NuGet packages on Server:
   - `Microsoft.AspNetCore.Authentication.JwtBearer`
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.Sqlite` (dev)
   - `Microsoft.EntityFrameworkCore.Tools`
   - `Swashbuckle.AspNetCore`
   - `CsvHelper`
7. Install NuGet packages on Client:
   - `Microsoft.AspNetCore.Components.Authorization`
   - `Blazored.LocalStorage`
8. **Configure CORS** on Server to allow Client origin.
9. **Configure Swagger** on Server (dev only).
10. Verify solution builds with no errors.

**Deliverable:** Three projects building cleanly. `dotnet build` passes.

---

## Phase 2: Domain Model and Database

**Goal:** Define all entities, configure EF Core, create migrations, and seed badge data.

### Steps

1. Create all enum files in `PedalAcrossCanada.Shared/Enums/`.
2. Create all entity classes in `PedalAcrossCanada.Server/Domain/Entities/`:
   - `Event`, `Milestone`, `Team`, `Participant`, `TeamHistory`
   - `Activity`, `Badge`, `BadgeAward`
   - `ExternalConnection`, `AuditLog`, `Notification`
3. Create `AppDbContext` with `DbSet<T>` for each entity.
4. Create `IEntityTypeConfiguration<T>` for each entity:
   - Set column types, constraints, indexes
   - Configure decimal precision `(10, 2)` for all distance columns
   - Configure unique constraints (e.g., Participant email per event)
5. Add ASP.NET Core Identity tables to `AppDbContext`.
6. Register `AppDbContext` in `Program.cs` with SQLite connection string.
7. Create initial migration: `dotnet ef migrations add InitialCreate`.
8. Apply migration on startup via `app.MigrateDatabase()` extension.
9. Seed default badges via `IHostedService` or `OnModelCreating HasData`.
10. Write unit tests for entity validation logic (pure domain rules, no DB required).

**Deliverable:** Database created with all tables. Badges seeded. Tests pass.

---

## Phase 3: Authentication and Identity

**Goal:** Stand up login, registration, JWT issuance, and role-based auth.

### Steps

1. Configure ASP.NET Core Identity in `Program.cs` with custom `IdentityUser`.
2. Create `AuthController` with:
   - `POST /api/auth/register`
   - `POST /api/auth/login` (returns accessToken + refreshToken)
   - `POST /api/auth/refresh`
   - `POST /api/auth/logout`
   - `GET /api/auth/me`
3. Implement `JwtTokenService` to generate/validate JWT with claims: `sub`, `email`, `role`.
4. Store refresh tokens in database (add `RefreshToken` entity or use Identity tokens).
5. Configure JWT Bearer middleware on Server.
6. Create the four application roles: Participant, TeamCaptain, Admin, ExecutiveViewer.
7. Seed an initial Admin user via `appsettings.Development.json` or environment variable.
8. **Client:** Implement `JwtAuthStateProvider` extending `AuthenticationStateProvider`.
9. **Client:** Implement `TokenService` for in-memory access token + localStorage refresh token.
10. **Client:** Implement `ApiClient` base service that injects Bearer token and handles 401 refresh.
11. **Client:** Create `Login.razor` page wired to `/api/auth/login`.
12. **Client:** Protect routes with `<AuthorizeView>` and `<AuthorizeRouteView>`.
13. **Client:** Redirect unauthenticated users to `/login`.
14. Write integration tests for login, refresh, and role enforcement.

**Deliverable:** Login works end-to-end. Protected pages redirect correctly. Admin can log in.

---

## Phase 4: Events and Milestones

**Goal:** Full CRUD for events and milestones with lifecycle enforcement.

### Steps

1. Create all shared DTOs in `PedalAcrossCanada.Shared/DTOs/Events/` and `DTOs/Milestones/`.
2. Implement `IEventService` and `EventService`:
   - Create, Read, Update event
   - Lifecycle transitions with all business rule enforcement
   - Audit logging on all mutations
3. Implement `IMilestoneService` and `MilestoneService`:
   - CRUD with ascending distance validation
   - Post-activation lock on cumulative km
   - Announcement workflow
4. Create `EventsController` and `MilestonesController`.
5. **Client:** Implement `EventService.cs` and `MilestoneService.cs` HTTP clients.
6. **Client:** Build `AdminEventSetup.razor` page with form and lifecycle buttons.
7. **Client:** Build `AdminMilestones.razor` with CRUD table.
8. **Client:** Build `Milestones.razor` public view with timeline.
9. **Client:** Build `EventStatusBanner` component.
10. Seed the Montreal-to-Calgary route milestones via a dev-only admin action or seed script.
11. Write tests: event lifecycle rules, milestone distance validation, activation guard.

**Deliverable:** Admin can create an event, add milestones, and activate it. Public milestone page renders.

---

## Phase 5: Teams and Participants

**Goal:** Team management and participant registration.

### Steps

1. Create shared DTOs for Teams and Participants.
2. Implement `ITeamService` / `TeamService` with audit logging.
3. Implement `IParticipantService` / `ParticipantService`:
   - Self-registration with duplicate prevention
   - Admin deactivate/reactivate
   - Team assignment with TeamHistory recording
4. Create `TeamsController` and `ParticipantsController`.
5. **Client:** Build `AdminTeams.razor`.
6. **Client:** Build `AdminParticipants.razor`.
7. **Client:** Build `JoinEvent.razor` (self-registration page).
8. **Client:** Build `MyProfile.razor` (display name, team, leaderboard opt-in).
9. Wire `ParticipantStateService` to cache own participant record after login.
10. Add `participantId` claim to JWT after participant registers.
11. Write tests: duplicate registration prevention, team change history, deactivation rules.

**Deliverable:** Employees can register, be assigned to teams. Admin can manage both.

---

## Phase 6: Manual Activity Entry and Approval

**Goal:** Core ride logging, duplicate warning, and admin approval workflow.

### Steps

1. Create shared DTOs for Activities.
2. Implement `IActivityService` / `ActivityService`:
   - Create manual activity with all validations (date range, max threshold, future date)
   - Duplicate candidate detection (same participant + date + ±10% distance)
   - Edit and delete with ownership + lock checks
   - Audit logging on all mutations
3. Implement `IApprovalService` / `ApprovalService`:
   - Approve: sets status, stores metadata, triggers totals recalculation
   - Reject: sets status, stores reason, creates Notification
4. Create `ActivitiesController` with all endpoints including approve/reject.
5. **Client:** Build `ActivityFormModal` component.
6. **Client:** Build `MyActivities.razor` page.
7. **Client:** Build `AdminActivities.razor` with approval queue tab.
8. Write tests: all validation rules, duplicate detection logic, approval/reject state transitions.

**Deliverable:** Participants can log rides. Admins can approve or reject pending entries.

---

## Phase 7: Totals, Leaderboards, and Dashboards

**Goal:** Calculated totals, rankings, and both dashboards.

### Steps

1. Implement `ILeaderboardService` / `LeaderboardService`:
   - Individual rankings with tie-breaking logic
   - Team rankings with average km
   - Respects `leaderboardPublic` setting
2. Implement `MilestoneCalculationService`:
   - Compare event total km to milestone thresholds
   - Mark newly achieved milestones, store metadata, create Notifications
3. Implement dashboard query methods in `DashboardService`.
4. Create `LeaderboardsController` and `DashboardsController`.
5. **Client:** Build `IndividualLeaderboardTable` and `TeamLeaderboardTable`.
6. **Client:** Build `Leaderboards.razor` page (tabbed).
7. **Client:** Build `EventProgressWidget` and `RouteProgressBar` components.
8. **Client:** Build `ParticipantDashboard.razor`.
9. **Client:** Build `EventHome.razor` with combined widgets.
10. Add `LeaderboardRefreshJob` and `MilestoneRecalculationJob` as Hangfire recurring jobs.
11. Wire milestone calculation to fire after every activity approval.
12. Write tests: tie-breaking, milestone threshold crossing, recalculation idempotency.

**Deliverable:** Live leaderboards and dashboards working. Milestones auto-achieve.

---

## Phase 8: Badges and Notifications

**Goal:** Badge engine, in-app notifications, milestone reward workflow.

### Steps

1. Implement `IBadgeService` / `BadgeService`:
   - Check participant km against all active badge thresholds after activity approval
   - Award with duplicate prevention
   - Honorary grant
2. Implement `INotificationService` / `NotificationService`.
3. Create `BadgesController` and `NotificationsController`.
4. **Client:** Build `BadgeDisplay` component.
5. **Client:** Build `NotificationBell` + `NotificationList` components with polling.
6. **Client:** Build `AdminBadges.razor`.
7. Wire badge checking to fire after every activity approval or import.
8. Wire milestone announcement workflow in `AdminMilestones.razor`.
9. Write tests: badge threshold detection, duplicate prevention, notification creation.

**Deliverable:** Badges auto-award. Notifications appear in-app for all key events.

---

## Phase 9: Reporting and Audit Log

**Goal:** CSV exports and audit trail viewer.

### Steps

1. Implement `ReportService` using `CsvHelper` to generate typed CSV streams.
2. Create `ReportsController` with all export endpoints and query filters.
3. Create `AuditController` with paginated read endpoint.
4. **Client:** Build `AdminReports.razor` with per-report filter forms and download triggers.
5. **Client:** Build `AdminAuditLog.razor` with filterable, paginated table and expandable rows.
6. Verify all prior mutation operations are audit logged correctly.
7. Write tests: CSV column correctness, report filtering, audit log query.

**Deliverable:** Admins can download all reports as CSV. Full audit trail is browsable.

---

## Phase 10: Strava Integration Scaffold

**Goal:** OAuth2 Strava linking and disconnect (no actual import yet).

### Steps

1. Register Strava API application at https://www.strava.com/settings/api.
2. Add Strava config to `appsettings.json`: `ClientId`, `ClientSecret`, `RedirectUri`.
3. Implement `StravaTokenService`:
   - Build authorization URL
   - Exchange code for tokens
   - Encrypt and store tokens via `TokenEncryptionService` (ASP.NET Data Protection)
4. Create `StravaController`: `auth-url`, `callback`, `disconnect`, `status`.
5. **Client:** Build Strava connect/disconnect section in `MyProfile.razor`.
6. **Client:** Build `StravaCallback.razor` page to handle redirect.
7. Check `Event.StravaEnabled` and feature flag before showing UI.
8. Write tests: token encryption round-trip, connection status transitions.

**Deliverable:** Participants can connect and disconnect Strava accounts. Tokens stored encrypted.

---

## Phase 11: Strava Activity Import

**Goal:** Import rides from Strava API into the activity system.

### Steps

1. Implement `StravaApiClient`:
   - GET athlete activities with date range filter
   - Handle token refresh (call `/oauth/token` with `refresh_token` grant)
   - Handle `RequiresReauth` state on refresh failure
2. Implement `StravaSyncJob`:
   - Fetch activities for each connected participant in active event
   - Filter by supported activity type (default: Ride) and event date range
   - Convert meters to km (÷ 1000, `Math.Round(..., 2, MidpointRounding.AwayFromZero)`)
   - Skip existing `ExternalActivityId` records
   - Create Activity records (source = Strava, status = Approved, countsTowardTotal = true)
   - Trigger badge check and milestone calculation after import
   - Log sync result; create Notification on failure
3. Register `StravaSyncJob` as Hangfire recurring job (every 30 min).
4. Create `POST /api/strava/sync` endpoint for participant-triggered manual sync.
5. Wire admin invalidate activity endpoint to set `countsTowardTotal = false`.
6. Write tests: duplicate skip logic, meter-to-km conversion, token refresh handling.

**Deliverable:** Connected participants' rides auto-import. Manual sync available.

---

## Phase 12: Duplicate Detection and Resolution

**Goal:** Flag and resolve suspected duplicate activities.

### Steps

1. Implement `IDuplicateService` / `DuplicateService`:
   - On manual activity creation: check same participant + date + ±10% distance
   - On Strava import: check same `ExternalActivityId`
   - Flag pairs; store `IsDuplicateFlagged = true` on both
2. Create `DuplicatesController` with list and resolve endpoints.
3. **Client:** Build `AdminDuplicates.razor` with side-by-side comparison cards.
4. Resolve actions trigger totals and milestone recalculation.
5. Audit log all resolution decisions.
6. Write tests: duplicate detection thresholds, resolution state transitions.

**Deliverable:** Admin can review and resolve flagged duplicate activities.

---

## Phase 13: Executive View, Polish, and NFR

**Goal:** Executive dashboard, final role enforcement review, performance check, and cleanup.

### Steps

1. **Client:** Build `ExecutiveDashboard.razor` (read-only summary, no admin controls).
2. Review all API endpoints for role enforcement completeness.
3. Review all pages for correct `<AuthorizeView Roles="...">` wrapping.
4. Implement `GlobalExceptionMiddleware` with ProblemDetails response.
5. Add `LoadingSpinner` to all async page loads.
6. Add `Toast` component to all mutation success/failure paths.
7. Performance review: add database indexes per `AuditLog` query patterns and Leaderboard queries.
8. Test dashboard load time with seeded data (1,000 participants, 50,000 activities).
9. Review token encryption and HTTPS enforcement.
10. Final end-to-end smoke test across all four personas.

**Deliverable:** Complete MVP ready for internal pilot. All AC items verified.

---

## Summary Timeline

| Phase | Focus | AC Items |
|---|---|---|
| 1 | Solution scaffold | - |
| 2 | Domain model + DB | DATA-01 |
| 3 | Auth + Identity | SEC-01 |
| 4 | Events + Milestones | EVT-01, EVT-02 |
| 5 | Teams + Participants | PAR-01, PAR-02 |
| 6 | Manual activities + approval | ACT-01, ACT-02, VAL-01 |
| 7 | Totals + Leaderboards + Dashboards | LEAD-01, LEAD-02, DASH-01, DASH-02 |
| 8 | Badges + Notifications | BADGE-01, REWARD-01, NOTIF-01 |
| 9 | Reporting + Audit | RPT-01, AUDIT-01 |
| 10 | Strava scaffold | STRAVA-01 |
| 11 | Strava import + background jobs | STRAVA-02, SYS-01 |
| 12 | Duplicate detection | ACT-03 |
| 13 | Executive view + polish + NFR | UI-01, NFR-01, API-01 |
