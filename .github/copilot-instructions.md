# Copilot Instructions

## Project Overview

**Cycle Challenge Tracker** — an internal company web app to track employee cycling kilometres during a company-wide biking initiative. Employees log rides manually or via Strava sync. Progress is measured against a virtual Montreal-to-Calgary route (~3,757 km). The app supports milestone celebrations, leaderboards, badges, and admin reporting.

Read `docs/00-main-acceptance-criteria.md` for the full feature specification before making any changes.

---

## Solution Structure

```
PedalAcrossCanada.sln
├── PedalAcrossCanada/           # Blazor WebAssembly client (.NET 10)
├── PedalAcrossCanada.Server/    # ASP.NET Core Web API (.NET 10)
└── PedalAcrossCanada.Shared/    # Shared DTOs and enums (.NET 10)
```

- **Client** calls **Server** via typed HttpClient services.
- **Shared** is referenced by both Client and Server. Put all DTOs and enums in Shared.
- **Server** owns all domain entities, EF Core, business logic, Identity, and background jobs.
- Do NOT put EF Core entities or business logic in the Client or Shared projects.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly, .NET 10, Bootstrap 5 |
| Backend | ASP.NET Core Web API, .NET 10 |
| ORM | Entity Framework Core 10 |
| Database (dev) | SQLite |
| Database (prod) | SQL Server |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Background jobs | Hangfire |
| CSV export | CsvHelper |
| API docs | Swashbuckle / OpenAPI |
| Token encryption | ASP.NET Core Data Protection |
| Client auth | Blazored.LocalStorage + custom AuthenticationStateProvider |
| Testing | xUnit (server), bUnit (client) |

---

## Coding Standards

### General
- Use `nullable` enabled everywhere. Annotate nullability explicitly.
- Use C# 13 features where appropriate (primary constructors, collection expressions, etc.).
- Use `async`/`await` for all I/O operations. Never use `.Result` or `.Wait()`.
- Prefer `IReadOnlyList<T>` or `IEnumerable<T>` for return types of collections from service interfaces.
- All dates stored as `DateTime` in UTC. Use `DateTime.UtcNow` everywhere. Never use `DateTime.Now`.
- All distances stored as `decimal` with precision `(10, 2)`.

### Naming
- Controllers: `{Entity}Controller.cs` (e.g., `ActivitiesController.cs`)
- Services: `{Entity}Service.cs` + `I{Entity}Service.cs` interface
- DTOs: `{Entity}Dto.cs`, `Create{Entity}Request.cs`, `Update{Entity}Request.cs`
- Blazor pages: PascalCase matching the route (e.g., `MyActivities.razor` for `/activities`)
- Blazor components: PascalCase describing the component purpose (e.g., `ActivityFormModal.razor`)

### Server
- All controllers inherit from `ApiControllerBase` (a custom base that sets `[ApiController]` and `[Route("api/[controller]")]`).
- Controllers are thin: delegate all logic to services via injected interfaces.
- Return `IActionResult` or `ActionResult<T>` from controller actions.
- Use `[ProducesResponseType]` attributes on all actions.
- Register services in `ServiceCollectionExtensions.cs`, not directly in `Program.cs`.
- Use `[Authorize(Roles = "Admin")]` on admin-only endpoints, not just `[Authorize]`.
- Return `ProblemDetails` for all error responses via `GlobalExceptionMiddleware`.
- Use `PagedResult<T>` for all paginated endpoints.

### EF Core
- Use `IEntityTypeConfiguration<T>` for all entity configuration. No data annotations on entities.
- Configure decimal precision with `.HasPrecision(10, 2)` in all entity configurations.
- Never use `int` primary keys. Use `Guid` for all entity PKs. Set default with `ValueGeneratedOnAdd`.
- Use `HasConversion` on all `DateTime` columns to ensure UTC is preserved on read from SQLite.
- Use `.AsNoTracking()` for all read-only queries.

### Shared DTOs
- All request DTOs use `record` types with `init` properties where possible.
- Validation attributes on request DTOs (`[Required]`, `[Range]`, `[MaxLength]`) for client-side validation in Blazor.
- Response DTOs use `class` types (serialization compatibility).

### Blazor Client
- All pages use `@inject` for services, not `[Inject]` attributes in code-behind.
- Use `<EditForm>` with `DataAnnotationsValidator` for all forms.
- Show a `LoadingSpinner` component while awaiting HTTP calls.
- Always show success/error feedback via the `Toast` component after mutations.
- Protect all pages that require auth with `@attribute [Authorize]` and specify roles where needed.
- Use `NavigationManager.NavigateTo` for programmatic navigation, not `<a href>`.
- Handle `HttpRequestException` gracefully; display user-friendly error messages, not raw exceptions.

### Audit Logging
- Every create, update, delete, and status change must create an `AuditLog` entry.
- Actor is the current user's `UserId` (from JWT claim `sub`). Use `"system"` for background jobs.
- Capture `BeforeSummary` (JSON snapshot before) and `AfterSummary` (JSON snapshot after) where applicable.
- Use `System.Text.Json.JsonSerializer.Serialize(entity)` for snapshots.
- Inject `IAuditService` into services that need to log. Do not log directly from controllers.

### Security
- Never log or return sensitive data (passwords, tokens, PII) in error messages or audit summaries.
- Strava tokens must be encrypted at rest using `IDataProtector`. Never store plaintext tokens.
- Validate that the authenticated user owns the resource before any mutation (participant editing own activity, etc.).
- Return `403 Forbidden` (not 404) when a user can see a resource exists but cannot act on it.

---

## Business Rules (Non-Negotiable)

1. **Only one Active event at a time.** Attempting to activate a second event returns 409 Conflict.
2. **Event cannot be activated without at least one milestone.**
3. **EndDate >= StartDate.** Validated on create and update.
4. **Distance > 0 and <= Event.MaxSingleRideKm.** Validated on activity create/edit.
5. **Activity date must be within event date range and not in the future.**
6. **Distances stored to 2 decimal places.** Use `Math.Round(value, 2, MidpointRounding.AwayFromZero)`.
7. **Strava meter-to-km conversion:** `Math.Round(meters / 1000m, 2, MidpointRounding.AwayFromZero)`.
8. **Duplicate external activity ids must be rejected silently** (skip, not error) during import.
9. **Badge awards are idempotent.** Check `BadgeAwards` table before inserting.
10. **Milestone cumulative km must be strictly ascending** within an event.
11. **Closed events are read-only to participants.** All participant mutation endpoints return 409 when event is Closed or Archived.
12. **Deactivated participants remain in all historical reports** but do not count in leaderboards.
13. **Only Approved and not-invalid activities count toward totals.**

---

## Leaderboard Tie-Breaking Order

When two participants have equal total km:
1. Higher ride count ranks first.
2. Earlier `JoinedAt` date ranks first.
3. If still tied: same rank number (no rank skipping for ties at same position).

---

## API Response Conventions

### Pagination
All list endpoints accept `?page=1&pageSize=25` and return:
```json
{ "data": [], "page": 1, "pageSize": 25, "totalCount": 0 }
```

### Validation Error (400)
```json
{ "errors": { "fieldName": ["Error message."] } }
```

### Success with warning (activity duplicate check)
```json
{ "data": { ... }, "duplicateWarning": true, "candidateActivityId": "guid" }
```

---

## File Locations Quick Reference

| What | Where |
|---|---|
| Domain entities | `PedalAcrossCanada.Server/Domain/Entities/` |
| Enums | `PedalAcrossCanada.Shared/Enums/` |
| DTOs | `PedalAcrossCanada.Shared/DTOs/` |
| EF config | `PedalAcrossCanada.Server/Infrastructure/Data/Configurations/` |
| Migrations | `PedalAcrossCanada.Server/Infrastructure/Data/Migrations/` |
| Server services | `PedalAcrossCanada.Server/Application/Services/` |
| Server interfaces | `PedalAcrossCanada.Server/Application/Interfaces/` |
| Background jobs | `PedalAcrossCanada.Server/Application/Jobs/` |
| Controllers | `PedalAcrossCanada.Server/Controllers/` |
| Client pages | `PedalAcrossCanada/Pages/` |
| Client components | `PedalAcrossCanada/Components/` |
| Client services | `PedalAcrossCanada/Services/` |
| Client auth | `PedalAcrossCanada/Auth/` |

---

## Seed Data

On first startup (dev and prod), seed these badge definitions if they don't exist:

| Name | ThresholdKm | IsDefault |
|---|---|---|
| First Ride | 0.01 | true |
| 50 km Club | 50 | true |
| Century Rider | 100 | true |
| Quarter Crusher | 250 | true |
| 500 km Legend | 500 | true |

Montreal-to-Calgary milestones: see `docs/00-main-acceptance-criteria.md` table. Seed via admin UI or a dev-only seed endpoint, not `HasData` (too many rows to maintain in migrations).

---

## Testing Guidance

- Write unit tests for all service methods with meaningful happy-path and edge-case coverage.
- Integration tests use `WebApplicationFactory<Program>` with SQLite in-memory.
- Each integration test class resets the database (use a fresh `AppDbContext` per test via factory).
- bUnit tests for Blazor components that contain significant logic or form validation.
- Test names: `MethodName_Scenario_ExpectedResult` (e.g., `CreateActivity_WhenDistanceExceedsMax_ReturnsBadRequest`).

---

## Documentation References

- `docs/00-main-acceptance-criteria.md` — Full AC pack (authoritative spec)
- `docs/01-epics.md` — Epic and story breakdown
- `docs/02-domain-model.md` — Entity definitions and relationships
- `docs/03-api-outline.md` — REST API endpoint list
- `docs/04-ui-pages.md` — Blazor page and component plan
- `docs/05-solution-architecture.md` — Technical architecture decisions
- `docs/06-build-order.md` — Phased implementation plan
