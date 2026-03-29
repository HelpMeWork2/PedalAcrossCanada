# 05 - Solution Architecture

## Overview

The application is structured as a three-project solution:

```
PedalAcrossCanada.sln
├── src/
│   ├── PedalAcrossCanada/               # Blazor WebAssembly client (existing)
│   ├── PedalAcrossCanada.Server/        # ASP.NET Core Web API + background jobs
│   └── PedalAcrossCanada.Shared/        # DTOs, enums, interfaces (referenced by both)
└── tests/
    ├── PedalAcrossCanada.Server.Tests/  # xUnit integration + unit tests
    └── PedalAcrossCanada.Client.Tests/  # bUnit component tests
```

---

## Project: PedalAcrossCanada (Client)

**SDK:** `Microsoft.NET.Sdk.BlazorWebAssembly`
**Target:** `net10.0`
**Purpose:** Browser-side SPA; communicates with Server API via HttpClient.

**Key NuGet packages to add:**
- `Microsoft.AspNetCore.Components.Authorization` - auth state management
- `Blazored.LocalStorage` - JWT token storage (access token in memory, refresh in local storage)

**Folder structure:**
```
PedalAcrossCanada/
  Layout/
    MainLayout.razor
    NavMenu.razor
    EventStatusBanner.razor
  Pages/
    Home.razor
    Login.razor
    JoinEvent.razor
    EventHome.razor
    ParticipantDashboard.razor
    MyActivities.razor
    Leaderboards.razor
    Milestones.razor
    MyProfile.razor
    Admin/
      AdminHub.razor
      AdminEventSetup.razor
      AdminMilestones.razor
      AdminTeams.razor
      AdminParticipants.razor
      AdminActivities.razor
      AdminDuplicates.razor
      AdminBadges.razor
      AdminReports.razor
      AdminAuditLog.razor
    Executive/
      ExecutiveDashboard.razor
    Strava/
      StravaCallback.razor
    NotFound.razor
    Unauthorized.razor
  Components/
    Shared/         (see 04-ui-pages.md)
    Activities/
    Leaderboards/
    Admin/
  Services/
    ApiClient.cs
    EventService.cs
    MilestoneService.cs
    TeamService.cs
    ParticipantService.cs
    ActivityService.cs
    LeaderboardService.cs
    DashboardService.cs
    BadgeService.cs
    NotificationService.cs
    StravaService.cs
    ReportService.cs
  State/
    EventStateService.cs
    ParticipantStateService.cs
    NotificationStateService.cs
  Auth/
    JwtAuthStateProvider.cs
    TokenService.cs
  wwwroot/
    index.html
    css/app.css
    lib/bootstrap/
```

---

## Project: PedalAcrossCanada.Server

**SDK:** `Microsoft.NET.Sdk.Web`
**Target:** `net10.0`
**Purpose:** RESTful Web API; hosts business logic, EF Core, Identity, background jobs, Strava OAuth.

**Key NuGet packages:**
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.SqlServer` (or `.Sqlite` for dev)
- `Microsoft.EntityFrameworkCore.Tools`
- `Hangfire.AspNetCore` + `Hangfire.SqlServer` - background job scheduler
- `CsvHelper` - CSV export generation
- `Swashbuckle.AspNetCore` - OpenAPI / Swagger

**Folder structure:**
```
PedalAcrossCanada.Server/
  Controllers/          (see 03-api-outline.md)
  Domain/
    Entities/           (see 02-domain-model.md)
    Enums/
  Application/
    Services/
      EventService.cs
      MilestoneService.cs
      TeamService.cs
      ParticipantService.cs
      ActivityService.cs
      ApprovalService.cs
      DuplicateService.cs
      LeaderboardService.cs
      MilestoneCalculationService.cs
      BadgeService.cs
      NotificationService.cs
      ReportService.cs
      AuditService.cs
    Interfaces/
      IEventService.cs
      IMilestoneService.cs
      ... (one per service)
    Jobs/
      StravaSync/
        StravaSyncJob.cs
        StravaApiClient.cs
        StravaTokenService.cs
      LeaderboardRefreshJob.cs
      MilestoneRecalculationJob.cs
  Infrastructure/
    Data/
      AppDbContext.cs
      Configurations/   (one IEntityTypeConfiguration per entity)
      Migrations/
      Seed/
        BadgeSeedData.cs
        MilestoneSeedData.cs
    Encryption/
      TokenEncryptionService.cs   (AES-256 for Strava tokens)
    Csv/
      CsvExportService.cs
  Middleware/
    GlobalExceptionMiddleware.cs
  Extensions/
    ServiceCollectionExtensions.cs
    WebApplicationExtensions.cs
  Program.cs
  appsettings.json
  appsettings.Development.json
```

---

## Project: PedalAcrossCanada.Shared

**SDK:** `Microsoft.NET.Sdk`
**Target:** `net10.0`
**Purpose:** Shared DTOs, enums, and validation attributes used by both Client and Server.

**Folder structure:**
```
PedalAcrossCanada.Shared/
  DTOs/
    Auth/
      LoginRequest.cs
      LoginResponse.cs
      RegisterRequest.cs
    Events/
      EventDto.cs
      CreateEventRequest.cs
      UpdateEventRequest.cs
    Milestones/
      MilestoneDto.cs
      CreateMilestoneRequest.cs
      UpdateMilestoneRequest.cs
    Teams/
      TeamDto.cs
      CreateTeamRequest.cs
    Participants/
      ParticipantDto.cs
      RegisterParticipantRequest.cs
      UpdateParticipantRequest.cs
    Activities/
      ActivityDto.cs
      CreateActivityRequest.cs
      UpdateActivityRequest.cs
      ApproveActivityRequest.cs
      RejectActivityRequest.cs
    Leaderboards/
      IndividualLeaderboardEntryDto.cs
      TeamLeaderboardEntryDto.cs
    Dashboards/
      EventDashboardDto.cs
      PersonalDashboardDto.cs
      AdminDashboardDto.cs
    Badges/
      BadgeDto.cs
      BadgeAwardDto.cs
      GrantBadgeRequest.cs
    Notifications/
      NotificationDto.cs
    Strava/
      StravaConnectionStatusDto.cs
    Reports/
      ReportFilterRequest.cs
    Audit/
      AuditLogEntryDto.cs
    Common/
      PagedResult.cs
      ValidationErrorResponse.cs
  Enums/           (same enums as Server Domain/Enums - single source of truth)
    EventStatus.cs
    ManualEntryMode.cs
    ActivitySource.cs
    ActivityStatus.cs
    RideType.cs
    ParticipantStatus.cs
    ConnectionStatus.cs
    AnnouncementStatus.cs
    NotificationType.cs
```

> **Note:** Server `Domain/Enums/` simply re-exports or references the Shared enums to avoid duplication.

---

## Authentication Flow

```
1. User submits login form
2. Client POSTs to /api/auth/login
3. Server validates credentials via ASP.NET Identity
4. Server returns { accessToken (JWT, 15 min), refreshToken (opaque, 7 days) }
5. Client stores:
   - accessToken: in-memory (JwtAuthStateProvider)
   - refreshToken: localStorage via Blazored.LocalStorage
6. ApiClient injects Authorization: Bearer <accessToken> on all requests
7. On 401 response: ApiClient auto-calls /api/auth/refresh with refreshToken
8. On successful refresh: replaces in-memory accessToken
9. On failed refresh: clears state, redirects to /login
```

**JWT Claims:**
- `sub` - UserId
- `email`
- `role` - one of: Participant, TeamCaptain, Admin, ExecutiveViewer
- `participantId` - event participant id (set after joining event)
- `eventId` - current active event id

---

## Background Jobs (Hangfire)

| Job | Schedule | Description |
|---|---|---|
| `StravaSyncJob` | Every 30 min | Sync all connected participants in active events |
| `LeaderboardRefreshJob` | Every 5 min | Recalculate and cache leaderboard totals |
| `MilestoneRecalculationJob` | Every 10 min | Check milestone thresholds against totals |

Jobs are registered in `Program.cs` via Hangfire's `IRecurringJobManager`.
Job failure is logged to `AuditLog` (actor = "system") and creates admin Notifications.

---

## Database Strategy

**Development:** SQLite (zero-config, file-based)
**Production:** SQL Server

Connection string in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=pedalacrosscanada.db"
  }
}
```

Connection string in `appsettings.json` (production placeholder):
```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=PedalAcrossCanada;..."
  }
}
```

**Migrations:** Run via `dotnet ef migrations add <Name> --project PedalAcrossCanada.Server`

**Seeding:**
- Default badges seeded on startup via `IHostedService` or `HasData` in `OnModelCreating`
- Route milestones NOT seeded automatically; admin creates via UI (or optional seed script)

---

## Strava Integration Architecture

```
Participant clicks "Connect Strava"
    -> Client calls GET /api/strava/auth-url
    -> Server builds Strava OAuth2 URL with state param (encrypted participantId)
    -> Client redirects browser to Strava
    -> Strava redirects to /api/strava/callback?code=...&state=...
    -> Server exchanges code for tokens
    -> Server encrypts tokens (AES-256) and saves ExternalConnection
    -> Server redirects to /strava/callback (client route) with success param
    -> Client route reads success param, shows toast, refreshes state
```

**Strava API base URL:** `https://www.strava.com/api/v3`
**Required scopes:** `activity:read` (or `activity:read_all` for private activities)
**Token encryption:** `TokenEncryptionService` using `IDataProtector` (ASP.NET Core Data Protection)

---

## CORS Configuration

Server allows requests from:
- `https://localhost:7xxx` (client dev port)
- Production client URL (environment variable)

---

## Error Handling

`GlobalExceptionMiddleware` catches all unhandled exceptions and returns:
```json
{
  "title": "An unexpected error occurred.",
  "status": 500,
  "traceId": "..."
}
```

Validation errors from FluentValidation or DataAnnotations return 400 with field-level messages.

---

## Feature Flags

Strava integration is controlled by:
1. `Event.StravaEnabled` (per-event)
2. `appsettings.json` → `Features:StravaEnabled` (global kill switch)

Client checks both before showing Strava UI elements.

---

## Deployment Target (MVP)

- Single server running both Server API and serving the WASM client static files
- Server project configured with `UseStaticFiles` and fallback to serve `wwwroot/index.html` for client routes
- OR: serve Client via CDN / Azure Static Web Apps + Server on Azure App Service
